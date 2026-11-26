// -----------------------------------------------------------------------------
// Crane Bay - Génération de l'APK de test.
//
// Depuis l'éditeur : menu "Crane Bay > Construire l'APK de test".
// En ligne de commande (sans ouvrir l'éditeur), voir README :
//   Unity -batchmode -quit -projectPath <dossier> -buildTarget Android \
//         -executeMethod CraneBay.EditorTools.BuildScript.BuildAndroidApk
// L'APK est produit dans Builds/Android/CraneBay.apk, signé avec la clé de
// debug de Unity (suffisant pour installer sur un téléphone en test).
// -----------------------------------------------------------------------------
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

        static bool Build(bool development)
        {
            ProjectSetup.Configure(openScene: false);

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
