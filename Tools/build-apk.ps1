# -----------------------------------------------------------------------------
# Crane Bay - Construit l'APK Android sans ouvrir l'éditeur Unity.
#
# Exemples (PowerShell, depuis n'importe où) :
#   E:\CraneBay-git\Tools\build-apk.ps1 -Version 0.1.2
#   E:\CraneBay-git\Tools\build-apk.ps1 -Version 0.1.2 -Release   # + préversion GitHub
#   ... -Release -Title "Bonus et séries" -NotesFile notes.md          # avec une vraie description
#
# Étapes : met le dépôt à jour depuis GitHub (branche main par défaut), lance
# Unity en mode batch, puis copie l'APK en Builds\Android\CraneBay_v<version>.apk.
# Avec -Release, publie l'APK en préversion GitHub (nécessite GitHub CLI "gh",
# connecté une fois avec "gh auth login").
#
# Le projet ne doit pas être ouvert dans l'éditeur Unity pendant le build
# (Unity refuse d'ouvrir deux fois le même dossier). Ce clone est séparé du
# dossier où vous travaillez dans l'éditeur, donc les deux peuvent coexister.
# -----------------------------------------------------------------------------
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Branch = "main",
    [string]$UnityExe = "E:\Unity\Editor\Unity.exe",
    [switch]$Release,
    # Titre court de la release (ex. "Bonus et séries") et notes en Markdown
    # sur ce qui change pour le joueur. Sans notes, une ligne générique est mise.
    [string]$Title = "",
    [string]$Notes = "",
    [string]$NotesFile = "",
    [switch]$NoUpdate
)

$ErrorActionPreference = "Stop"
$Version = $Version.TrimStart("v", "V")
$ProjectPath = Split-Path -Parent $PSScriptRoot
$LogFile = Join-Path $ProjectPath "build.log"
$Apk = Join-Path $ProjectPath "Builds\Android\CraneBay.apk"
$FinalApk = Join-Path $ProjectPath "Builds\Android\CraneBay_v$Version.apk"

if (-not (Test-Path $UnityExe)) { throw "Unity introuvable : $UnityExe (utilisez -UnityExe <chemin>)" }

if (-not $NoUpdate) {
    Write-Host "== Mise à jour du dépôt ($Branch)"
    git -C $ProjectPath fetch origin $Branch
    if ($LASTEXITCODE -ne 0) { throw "git fetch a échoué" }
    git -C $ProjectPath checkout -B $Branch "origin/$Branch"
    if ($LASTEXITCODE -ne 0) { throw "git checkout a échoué" }
}
$Commit = (git -C $ProjectPath rev-parse --short HEAD)

Write-Host "== Build Unity v$Version (commit $Commit), journal : $LogFile"
Write-Host "   (la première fois, l'import et IL2CPP peuvent prendre 10 à 20 minutes)"
if (Test-Path $Apk) { Remove-Item $Apk }
$unityArgs = @("-batchmode", "-quit", "-projectPath", "`"$ProjectPath`"", "-buildTarget", "Android",
    "-executeMethod", "CraneBay.EditorTools.BuildScript.BuildAndroidApk",
    "-appVersion", $Version, "-logFile", "`"$LogFile`"")
$p = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow

# Sur ce PC, Gradle échoue parfois au démarrage depuis Unity en mode batch
# ("BindException: Address already in use" dans FileLockContentionHandler).
# Le projet Gradle est alors déjà généré : on relance Gradle directement.
if (($p.ExitCode -ne 0 -or -not (Test-Path $Apk)) -and (Select-String -Path $LogFile -Pattern "BindException" -Quiet)) {
    $unityDir = Split-Path -Parent $UnityExe
    $java = Join-Path $unityDir "Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\java.exe"
    $launcherJar = Get-ChildItem (Join-Path $unityDir "Data\PlaybackEngines\AndroidPlayer\Tools\gradle\lib") -Filter "gradle-launcher-*.jar" | Select-Object -First 1
    $gradleDir = Join-Path $ProjectPath "Library\Bee\Android\Prj\IL2CPP\Gradle\launcher"
    if ((Test-Path $java) -and $launcherJar -and (Test-Path $gradleDir)) {
        Write-Host "== Gradle a échoué au démarrage (BindException) : relance directe de Gradle"
        Push-Location $gradleDir
        & $java -classpath $launcherJar.FullName org.gradle.launcher.GradleMain "-Dorg.gradle.jvmargs=-Xmx4096m" --no-daemon assembleRelease *> (Join-Path $ProjectPath "gradle.log")
        $gradleExit = $LASTEXITCODE
        Pop-Location
        $built = Join-Path $gradleDir "build\outputs\apk\release\launcher-release.apk"
        if ($gradleExit -eq 0 -and (Test-Path $built)) {
            New-Item -ItemType Directory -Force (Split-Path -Parent $Apk) | Out-Null
            Copy-Item $built $Apk -Force
            $p = [pscustomobject]@{ ExitCode = 0 }
        }
    }
}

if ($p.ExitCode -ne 0 -or -not (Test-Path $Apk)) {
    Write-Host "== ÉCHEC (code $($p.ExitCode)). Dernières lignes du journal :" -ForegroundColor Red
    Get-Content $LogFile -Tail 40
    exit 1
}

Copy-Item $Apk $FinalApk -Force
$size = "{0:N1}" -f ((Get-Item $FinalApk).Length / 1MB)
Write-Host "== APK prêt : $FinalApk ($size Mo)" -ForegroundColor Green

if ($Release) {
    # gh peut manquer dans le PATH d'une session ouverte avant son installation.
    $gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
    if (-not $gh -and (Test-Path "$env:ProgramFiles\GitHub CLI\gh.exe")) { $gh = "$env:ProgramFiles\GitHub CLI\gh.exe" }
    if (-not $gh) {
        throw "GitHub CLI absent : installez-le (winget install GitHub.cli) puis 'gh auth login'."
    }
    $repo = "nanou974/crane-bay"
    Write-Host "== Publication de la préversion $Version sur $repo"
    $fullTitle = if ($Title) { "v$Version : $Title" } else { "Pre-release v$Version" }
    if ($NotesFile) { $Notes = [IO.File]::ReadAllText((Resolve-Path $NotesFile)) }
    if (-not $Notes) { $Notes = "APK de test construit automatiquement." }
    $Notes = $Notes.TrimEnd() + "`n`n_Construit depuis $Branch (commit $Commit)._`n"
    $tmpNotes = Join-Path $env:TEMP "cranebay-notes-$Version.md"
    [IO.File]::WriteAllText($tmpNotes, $Notes)
    & $gh release create $Version $FinalApk --repo $repo --target $Branch --prerelease `
        --title $fullTitle --notes-file $tmpNotes
    if ($LASTEXITCODE -ne 0) { throw "gh release create a échoué" }
}
