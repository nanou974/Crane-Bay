# -----------------------------------------------------------------------------
# Crane Bay - Construit l'APK Android sans ouvrir l'éditeur Unity.
#
# Exemples (PowerShell, depuis n'importe où) :
#   E:\CraneBay-git\Tools\build-apk.ps1 -Version 0.1.2
#   E:\CraneBay-git\Tools\build-apk.ps1 -Version 0.1.2 -Release   # + préversion GitHub
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

if ($p.ExitCode -ne 0 -or -not (Test-Path $Apk)) {
    Write-Host "== ÉCHEC (code $($p.ExitCode)). Dernières lignes du journal :" -ForegroundColor Red
    Get-Content $LogFile -Tail 40
    exit 1
}

Copy-Item $Apk $FinalApk -Force
$size = "{0:N1}" -f ((Get-Item $FinalApk).Length / 1MB)
Write-Host "== APK prêt : $FinalApk ($size Mo)" -ForegroundColor Green

if ($Release) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI absent : installez-le (winget install GitHub.cli) puis 'gh auth login'."
    }
    $repo = "nanou974/crane-bay"
    Write-Host "== Publication de la préversion $Version sur $repo"
    gh release create $Version $FinalApk --repo $repo --target $Branch --prerelease `
        --title "Pre-release v$Version" --notes "APK de test construit automatiquement depuis $Branch (commit $Commit)."
    if ($LASTEXITCODE -ne 0) { throw "gh release create a échoué" }
}
