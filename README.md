# Crane Bay

Jeu mobile Android de gestion de terminal à conteneurs, vu de dessus. Développé avec Unity (C#).
Gratuit, sans publicité ni achat intégré, distribué en APK hors store.

> **État : prototype jouable (v0.1).** La boucle complète est en place (navire → parc → camion/train →
> argent → amélioration), avec les coffres, la sauvegarde et les contrôles tactiles. Les graphismes sont
> des formes dessinées par code (aucun asset externe) : ils servent de base lisible en attendant de vrais
> sprites.

## Le jeu en bref

1. Un **porte-conteneurs** arrive à quai (en haut de l'écran) avec 6 à 12 conteneurs.
2. Le joueur commande le **portique** pour déposer chaque conteneur dans le **parc** (5 × 4 emplacements,
   3 conteneurs maximum par pile).
3. **Camions** (2 places sur la route) et **trains** (3 wagons) arrivent et demandent une couleur précise,
   affichée en silhouette sur la remorque ou le wagon, avec sa valeur.
4. Chaque conteneur livré rapporte de l'argent : blanc 10 $, bleu 25 $, vert 50 $, rouge 100 $, doré 250 $
   (+50 % en train).
5. L'argent achète des améliorations à 10 niveaux, au coût croissant : **vitesse de la grue**, **attente
   entre deux navires**, **attente entre deux camions/trains**.

**Le casse-tête :** la grue ne peut saisir que le conteneur du dessus d'une pile. Pour sortir un conteneur
enfoui, il faut d'abord déplacer ceux qui sont au-dessus. Un véhicule qui attend trop longtemps (jauge de
patience) repart à vide.

**Niveaux :** chaque conteneur livré rapporte des XP (1 à 12 selon la couleur, +50 % en train). Quand la
barre d'XP est pleine, on passe au niveau suivant (30 niveaux, seuil croissant) :
- prime en argent (50 $ × le nouveau niveau) ;
- **le terminal se réinitialise** : parc et navire vidés, véhicules renvoyés, un nouveau navire arrive.
  L'argent, les améliorations et les coffres sont conservés ;
- de nouvelles couleurs apparaissent : vert au niveau 2, rouge au niveau 4, doré au niveau 6. Au niveau 1,
  il n'y a que du blanc et du bleu ;
- les navires arrivent plus chargés : +1 conteneur tous les 3 niveaux.

Les règles sont dans `Core/LevelRules.cs`.

**Coffres :**
- *Coffre du jour* : toutes les 24 h, de l'argent et parfois un turbo de grue (x2 pendant 5 min).
- *Coffre 20 min* : de l'argent ou un turbo de 2 min. Il se recharge aussi quand le jeu est fermé.
- Le jeu enregistre l'heure UTC de la dernière ouverture, pas un compte à rebours : le timer reste exact
  après une fermeture ou un redémarrage du téléphone.

**Contrôles :** toucher une pile puis toucher la destination, ou glisser-déposer du doigt. Les destinations
possibles s'allument en vert. Le bouton retour d'Android annule la sélection.

**Orientation : portrait**, pour jouer à une main. Pour passer en paysage, il faut modifier
`ProjectSetup.cs` (orientation) et `BoardLayout.cs` (disposition).

## Prérequis

- **Unity 6 LTS** (série 6000.x), installé avec Unity Hub.
- Module **Android Build Support**, avec **OpenJDK** et **Android SDK & NDK Tools** cochés. On l'ajoute
  dans Unity Hub > Installs > ⚙ > Add modules.

Le projet n'ajoute aucun package en dehors de uGUI et des modules standard (voir `Packages/manifest.json`).

## Ouvrir le projet

1. Cloner le dépôt.
2. Dans Unity Hub, cliquer sur **Add > Add project from disk** et choisir le dossier du dépôt.
   Choisir une version Unity 6 LTS si le Hub en demande une.
3. Au premier chargement, Unity génère `Library/` et `ProjectSettings/`, puis le script
   `ProjectSetup` configure tout seul le projet :
   - il crée la scène `Assets/Scenes/Main.unity` et l'ajoute au build ;
   - il règle Android : portrait, paquet `com.cranebay.prototype`, IL2CPP + ARM64, Gamma, APK (pas AAB).

   Ces réglages peuvent être relancés à tout moment par le menu **Crane Bay > Configurer le projet**.
4. Dans l'onglet **Game**, choisir une résolution portrait (par exemple 1080 × 1920), puis lancer **Play**.
   Dans l'éditeur, la souris remplace le doigt.

## Générer un APK de test

### Depuis l'éditeur

Menu **Crane Bay > Construire l'APK de test**.
Le script passe la plateforme sur Android si besoin (la première fois, cela peut prendre quelques minutes),
puis produit **`Builds/Android/CraneBay.apk`**, signé avec la clé de debug de Unity.

La variante *(mode développement)* ajoute le profiler et les logs (`adb logcat -s Unity`).

### En ligne de commande

```bash
# Windows : "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
# macOS   : /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
Unity -batchmode -quit -projectPath /chemin/vers/crane-bay -buildTarget Android \
      -executeMethod CraneBay.EditorTools.BuildScript.BuildAndroidApk -logFile build.log
```

Le code de sortie vaut 0 si le build réussit, 1 sinon (le détail est dans `build.log`).

### Installer sur le téléphone

- **Par câble :** activer le *débogage USB* (Options pour les développeurs), puis
  `adb install -r Builds/Android/CraneBay.apk`.
- **Sans câble :** copier l'APK sur le téléphone, l'ouvrir depuis le gestionnaire de fichiers et autoriser
  « Installer des applications inconnues » pour cette application.

Pour une vraie diffusion hors store, remplacer la clé de debug par un keystore personnel
(Player Settings > Publishing Settings) et le conserver précieusement : toutes les mises à jour devront
être signées avec la même clé.

## Organisation du code

Tous les scripts sont commentés en français.

```
Assets/CraneBay/Scripts/
├── Core/            Logique pure, SANS dépendance Unity (testable hors moteur)
│   ├── GameRules.cs     Équilibrage : valeurs, améliorations, timers, tailles du parc
│   ├── StackGrid.cs     Piles de conteneurs : on ne prend que le dessus, hauteur max
│   ├── PortModel.cs     Règles du terminal : navires, véhicules, demandes, paiement, coffres
│   ├── LevelRules.cs    Niveaux : XP par couleur, seuils, primes, couleurs débloquées
│   ├── ChestLogic.cs    Timers de coffres basés sur l'horloge UTC, récompenses
│   ├── SaveData.cs      Données sauvegardées + réparation des sauvegardes anciennes
│   └── ContainerTypes.cs
├── Game/            Couche Unity (affichage, entrées, sons)
│   ├── GameManager.cs   Chef d'orchestre : relie le modèle aux vues, sauvegarde auto
│   ├── CraneController.cs  Portique : déplacements fluides, file d'ordres, levage
│   ├── InputController.cs  Tap-tap et glisser-déposer
│   ├── Hud.cs           Interface uGUI construite par code (safe area gérée)
│   ├── ShipView.cs, VehicleView.cs, ContainerView.cs, WorldBuilder.cs
│   ├── SpriteFactory.cs Sprites générés dans un atlas unique (contours nets, ombres douces)
│   ├── AudioManager.cs  Bruitages synthétisés (pas de fichiers audio)
│   ├── SaveSystem.cs    JSON local, écriture atomique
│   └── Bootstrap.cs     Lance le jeu même depuis une scène vide
└── Editor/
    ├── ProjectSetup.cs  Scène + réglages Android + menus de test
    └── BuildScript.cs   Génération de l'APK (menu et ligne de commande)
Tests/CoreTests/     Tests .NET de la logique (sans Unity)
```

Toute la scène est construite par code au lancement. C'est voulu : il n'y a ni prefab ni scène à
maintenir à la main, et le projet s'ouvre proprement quelle que soit la version de Unity 6.

### Équilibrage

Tous les chiffres sont dans `Core/GameRules.cs` : valeur et rareté des couleurs, coût de base et
croissance des améliorations, attentes entre navires et véhicules, durée des coffres. La patience des
véhicules se règle dans `PortModel.cs` (`TruckPatience`, `TrainPatience`).

### Sauvegarde

Le fichier `cranebay_save.json` est écrit dans `Application.persistentDataPath`. Sur Android, c'est
`Android/data/com.cranebay.prototype/files/`. Il contient l'argent, le niveau et les XP, les niveaux d'amélioration, le parc,
le navire à quai, les horodatages des coffres et du prochain navire. La sauvegarde a lieu toutes les
10 s, après chaque achat ou coffre, et quand l'application passe en arrière-plan.

Les camions et trains en attente ne sont pas sauvegardés : de nouveaux arrivent au lancement suivant.
Les conteneurs déjà livrés ont été payés, donc rien n'est perdu.

Menus utiles dans l'éditeur : **Crane Bay > Test : effacer la sauvegarde**, **+1 000 $**,
**recharger les coffres**, **ouvrir le dossier de sauvegarde**.

### Performances (Android d'entrée de gamme)

- 60 FPS visés (`targetFrameRate = 60`), pas de physique, pas de lumière, pipeline intégré (Built-in).
- Tous les sprites viennent d'une seule texture 256 × 256, ce qui permet de les regrouper en peu de draw calls.
- Espace colorimétrique Gamma, aucune allocation par image dans la boucle principale (textes et indicateurs
  réutilisés).

## Tests

La logique du jeu (règles d'empilage, timers des coffres hors ligne, coûts des améliorations, réparation de
sauvegarde, et **une partie complète simulée** : navire → parc → livraison → achat d'amélioration) est testée
sans Unity :

```bash
dotnet run --project Tests/CoreTests
```

Ces tests tournent aussi automatiquement sur GitHub (Actions) à chaque push.

## Limites connues du prototype

- Graphismes et sons procéduraux (placeholders lisibles), à remplacer par de vrais assets.
- Un seul portique, un seul poste à quai, un seul gabarit de conteneur (40 pieds stylisé).
- Pas de menu titre ni de réglages en dehors du bouton son.
