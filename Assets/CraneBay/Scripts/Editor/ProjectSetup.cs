// -----------------------------------------------------------------------------
// Crane Bay - Configuration automatique du projet dans l'éditeur Unity.
//
// Au premier chargement du projet (et via le menu "Crane Bay > Configurer le
// projet"), ce script :
//  - crée la scène Assets/Scenes/Main.unity (caméra + GameManager) ;
//  - l'ajoute aux scènes du build ;
//  - règle les paramètres Android : orientation PORTRAIT, identifiant du
//    paquet, IL2CPP + ARM64, espace colorimétrique Gamma (moins coûteux),
//    ancien Input Manager (utilisé par les contrôles tactiles) ;
//  - pose l'icône de l'appli (Assets/CraneBay/Icons) : icône adaptative
//    Android 8+ (fond + premier plan), icône ronde et icône classique.
// Les réglages sont faits par code plutôt que dans des fichiers
// ProjectSettings écrits à la main : c'est plus robuste d'une version de Unity
// à l'autre.
// -----------------------------------------------------------------------------
using System.IO;
using CraneBay.Game;
using UnityEditor;
using UnityEditor.Android;
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
            ApplyIcons();
        }

        // ---------------------------------------------------------------------
        // Icône de l'appli
        // ---------------------------------------------------------------------

        const string IconFolder = "Assets/CraneBay/Icons/";

        /// <summary>
        /// Remplace l'icône Unity par celle de Crane Bay. Les PNG sont générés
        /// depuis des SVG (voir Assets/CraneBay/Icons/LISEZMOI.txt) :
        ///  - adaptative (Android 8+) : fond + premier plan 432x432, le lanceur
        ///    découpe la forme (rond, carré arrondi...) ;
        ///  - ronde et classique (anciens Android) : image complète 512x512.
        /// </summary>
        static void ApplyIcons()
        {
            var background = LoadIcon("icon_adaptive_background.png");
            var foreground = LoadIcon("icon_adaptive_foreground.png");
            var legacy = LoadIcon("icon_legacy.png");
            var round = LoadIcon("icon_round.png");
            if (background == null || foreground == null || legacy == null || round == null)
            {
                Debug.LogWarning("[CraneBay] Icônes introuvables dans " + IconFolder + " : icône Unity conservée.");
                return;
            }

            // Icône par défaut (toutes plateformes).
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { legacy });

            var android = NamedBuildTarget.Android;
            SetAndroidIcons(android, AndroidPlatformIconKind.Adaptive, background, foreground);
            SetAndroidIcons(android, AndroidPlatformIconKind.Round, round);
            SetAndroidIcons(android, AndroidPlatformIconKind.Legacy, legacy);
        }

        /// <summary>Même(s) texture(s) pour toutes les densités : Unity redimensionne.</summary>
        static void SetAndroidIcons(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        /// <summary>Charge un PNG d'icône en le réglant sans compression ni mipmaps.</summary>
        static Texture2D LoadIcon(string file)
        {
            string path = IconFolder + file;
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool changed = importer.textureType != TextureImporterType.Default
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None
                    || !importer.alphaIsTransparency;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
