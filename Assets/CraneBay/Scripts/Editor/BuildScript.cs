// -----------------------------------------------------------------------------
// Crane Bay - Génération de l'APK de test.
//
// Depuis l'éditeur : menu "Crane Bay > Construire l'APK de test".
// En ligne de commande (sans ouvrir l'éditeur), voir README :
//   Unity -batchmode -quit -projectPath <dossier> -buildTarget Android \
//         -executeMethod CraneBay.EditorTools.BuildScript.BuildAndroidApk \
//         [-appVersion 0.1.2]
// Sous Windows, Tools/build-apk.ps1 fait tout (mise à jour git + build + copie).
// L'APK est produit dans Builds/Android/CraneBay.apk, signé avec la clé de
// debug de Unity (suffisant pour installer sur un téléphone en test).
// -----------------------------------------------------------------------------
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CraneBay.EditorTools
{
    public static class BuildScript
    {
        public const string OutputPath = "Builds/Android/CraneBay.apk";

        [MenuItem("Crane Bay/Construire l'APK de test", priority = 20)]
        public static void BuildFromMenu()
        {
            if (Build(development: false)) EditorUtility.RevealInFinder(OutputPath);
        }

        [MenuItem("Crane Bay/Construire l'APK de test (mode développement)", priority = 21)]
        public static void BuildDevFromMenu()
        {
            if (Build(development: true)) EditorUtility.RevealInFinder(OutputPath);
        }

        /// <summary>Point d'entrée pour -executeMethod (build automatisé).</summary>
        public static void BuildAndroidApk()
        {
            bool ok = Build(development: false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// "-appVersion 0.1.2" : fixe la version affichée et un versionCode croissant
        /// (0.1.2 -> 102) pour qu'Android accepte l'APK comme mise à jour.
        /// </summary>
        static void ApplyVersionFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-appVersion") continue;
                string version = args[i + 1].TrimStart('v', 'V');
                string[] parts = version.Split('.');
                int code = 0;
                for (int p = 0; p < 3; p++)
                {
                    int n = 0;
                    if (p < parts.Length && !int.TryParse(parts[p], out n))
                    {
                        Debug.LogError("[CraneBay] Version invalide : " + version + " (attendu X.Y.Z)");
                        return;
                    }
                    code = code * 100 + n;
                }
                PlayerSettings.bundleVersion = version;
                PlayerSettings.Android.bundleVersionCode = Math.Max(1, code);
                Debug.Log($"[CraneBay] Version {version} (versionCode {PlayerSettings.Android.bundleVersionCode})");
                return;
            }
        }

        static bool Build(bool development)
        {
            ProjectSetup.Configure(openScene: false);
            ApplyVersionFromCommandLine();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // Bascule de plateforme (peut prendre quelques minutes la première fois).
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[CraneBay] Impossible de passer sur Android : installez le module 'Android Build Support' (avec SDK/NDK et OpenJDK) via Unity Hub.");
                    return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[CraneBay] APK généré : {OutputPath} ({summary.totalSize / (1024 * 1024f):0.0} Mo)");
                return true;
            }
            Debug.LogError("[CraneBay] Échec du build : " + summary.result + " (" + summary.totalErrors + " erreur(s), voir la console)");
            return false;
        }
    }
}
