// -----------------------------------------------------------------------------
// Crane Bay - Configuration automatique du projet dans l'éditeur Unity.
//
// Au premier chargement du projet (et via le menu "Crane Bay > Configurer le
// projet"), ce script :
//  - crée la scène Assets/Scenes/Main.unity (caméra + GameManager) ;
//  - l'ajoute aux scènes du build ;
//  - règle les paramètres Android : orientation PORTRAIT, identifiant du
//    paquet, IL2CPP + ARM64, espace colorimétrique Gamma (moins coûteux),
//    ancien Input Manager (utilisé par les contrôles tactiles).
// Les réglages sont faits par code plutôt que dans des fichiers
// ProjectSettings écrits à la main : c'est plus robuste d'une version de Unity
// à l'autre.
// -----------------------------------------------------------------------------
using System.IO;
using CraneBay.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraneBay.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string PackageId = "com.cranebay.prototype";
        const string DoneMarker = "ProjectSettings/CraneBaySetup.txt";

        static ProjectSetup()
        {
            // Attendre que l'éditeur ait fini de charger avant de toucher aux scènes.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(DoneMarker) && File.Exists(ScenePath)) return;
                Configure(openScene: true);
            };
        }

        [MenuItem("Crane Bay/Configurer le projet", priority = 0)]
        public static void ConfigureMenu() => Configure(openScene: true);

        /// <summary>Applique tous les réglages (sans danger si appelé plusieurs fois).</summary>
        public static void Configure(bool openScene)
        {
            ApplyPlayerSettings();
            EnsureScene(openScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            File.WriteAllText(DoneMarker, "Configuration Crane Bay appliquée. Supprimer ce fichier pour la relancer au prochain chargement.\n");
            AssetDatabase.SaveAssets();
            Debug.Log("[CraneBay] Projet configuré : scène " + ScenePath + ", Android portrait, " + PackageId);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Crane Bay";
            PlayerSettings.productName = "Crane Bay";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);

            // Orientation : portrait (jeu à une main). Pour passer en paysage,
            // remplacer Portrait par LandscapeLeft (et adapter BoardLayout).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Performances sur Android d'entrée de gamme.
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = false; // aucun accès réseau
            PlayerSettings.runInBackground = false;

            // Un APK (et non un .aab) pour la distribution hors store.
            EditorUserBuildSettings.buildAppBundle = false;

            ForceLegacyInputManager();
        }

        /// <summary>
        /// Les contrôles utilisent UnityEngine.Input (ancien Input Manager).
        /// Si le projet est réglé sur "Input System Package (New)" uniquement,
        /// on passe sur "Both" pour éviter toute exception au lancement.
        /// </summary>
        static void ForceLegacyInputManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 1)
            {
                prop.intValue = 2; // 0 = ancien, 1 = nouveau, 2 = les deux
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.LogWarning("[CraneBay] Gestion des entrées passée sur 'Both'. Redémarrez Unity si l'éditeur le demande.");
            }
        }

        static void EnsureScene(bool openScene)
        {
            if (File.Exists(ScenePath))
            {
                if (openScene && SceneManager.GetActiveScene().path != ScenePath &&
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.49f, 0.8f, 0.42f);
            camGo.transform.position = new Vector3(0f, 0.4f, -10f);
            camGo.AddComponent<AudioListener>();

            new GameObject("CraneBay").AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------------
        // Outils de test (menu Crane Bay)
        // ---------------------------------------------------------------------

        [MenuItem("Crane Bay/Test : effacer la sauvegarde", priority = 50)]
        static void DeleteSave()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CraneBay] Arrêtez le mode Play avant d'effacer la sauvegarde (sinon elle sera réécrite).");
                return;
            }
            SaveSystem.Delete();
            Debug.Log("[CraneBay] Sauvegarde effacée : " + SaveSystem.FilePath);
        }

        [MenuItem("Crane Bay/Test : +1 000 $ (en jeu)", priority = 51)]
        static void AddMoney()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogWarning("[CraneBay] Lancez le jeu (Play) d'abord."); return; }
            gm.Model.Data.money += 1000;
        }

        [MenuItem("Crane Bay/Test : recharger les coffres (en jeu)", priority = 52)]
        static void RefillChests()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogWarning("[CraneBay] Lancez le jeu (Play) d'abord."); return; }
            gm.Model.Data.lastDailyChestUnix = 0;
            gm.Model.Data.lastQuickChestUnix = 0;
        }

        [MenuItem("Crane Bay/Ouvrir le dossier de sauvegarde", priority = 53)]
        static void RevealSave() => EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
