// -----------------------------------------------------------------------------
// Crane Bay - Données sauvegardées (format JSON via JsonUtility).
// Contient tout ce qui doit survivre à la fermeture du jeu : argent, niveaux
// d'amélioration, état du parc et du navire, horodatages des coffres.
// Les horodatages sont en secondes Unix UTC (double) pour rester exacts même
// si le jeu est fermé ou si le fuseau horaire change.
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;

        // --- Économie ---
        public long money;
        public long totalEarned;
        public int delivered;                 // nombre de conteneurs livrés (statistique)
        public int[] upgrades = new int[GameRules.UpgradeCount]; // index = UpgradeType

        // --- Niveau du joueur ---
        public int level = 1;
        public long xp;                       // XP accumulées dans le niveau en cours

        // --- Parc et navire ---
        public StackGrid yard;
        public StackGrid ship;
        public bool shipDocked;
        public double nextShipUnix;           // heure d'arrivée du prochain navire
        public int nextContainerId = 1;

        // --- Coffres et bonus ---
        public double lastDailyChestUnix;     // 0 = jamais ouvert (donc prêt)
        public double lastQuickChestUnix;
        public double turboEndUnix;           // fin du turbo de grue

        // --- Contrats du jour ---
        public long contractsDay = -1;        // jour (UTC) des contrats en cours
        public List<Contract> contracts = new List<Contract>();

        // --- Statistiques et records ---
        public double shipDockedSinceUnix;    // arrivée du navire en cours (record de déchargement)
        public int shipsUnloaded;
        public int trainsCompleted;
        public int fastDeliveries;
        public int bestTrainEarned;
        public double bestUnloadSeconds;      // 0 = pas encore de record
        public int bestSingleDelivery;

        // Bilan du niveau en cours (étoiles de fin de niveau), remis à zéro à chaque niveau.
        public int levelDelivered;
        public int levelFast;
        public int levelMissed;               // véhicules repartis sans être complets

        // Série de jours du coffre du jour (1 à 7, puis recommence).
        public int dailyStreak;

        // Cartes de fin de niveau encore à choisir (une par niveau franchi).
        public int pendingCards;

        // Mini-jeu de détente « sortez le conteneur rouge ».
        public int puzzlesSolved;             // numéro du prochain puzzle
        public int puzzlesPerfect;            // résolus en un minimum de coups

        public static SaveData NewGame(double nowUnix)
        {
            return new SaveData
            {
                yard = new StackGrid(GameRules.YardColumns, GameRules.YardRows, GameRules.YardMaxHeight),
                ship = new StackGrid(GameRules.ShipColumns, GameRules.ShipRows, GameRules.ShipMaxHeight),
                shipDocked = false,
                nextShipUnix = nowUnix + 1.5, // premier navire presque tout de suite
            };
        }

        /// <summary>
        /// Répare une sauvegarde incomplète ou d'une ancienne version (champs
        /// manquants, dimensions de grille modifiées, valeurs hors bornes).
        /// </summary>
        public void Sanitize(double nowUnix)
        {
            levelDelivered = Math.Max(0, levelDelivered);
            levelFast = Math.Max(0, Math.Min(levelFast, levelDelivered));
            levelMissed = Math.Max(0, levelMissed);
            dailyStreak = Math.Max(0, dailyStreak);
            pendingCards = Math.Max(0, Math.Min(5, pendingCards));
            puzzlesSolved = Math.Max(0, puzzlesSolved);
            puzzlesPerfect = Math.Max(0, Math.Min(puzzlesSolved, puzzlesPerfect));
            int n = GameRules.UpgradeCount;
            if (upgrades == null || upgrades.Length != n)
            {
                var fixedUp = new int[n];
                if (upgrades != null)
                    for (int i = 0; i < Math.Min(n, upgrades.Length); i++) fixedUp[i] = upgrades[i];
                upgrades = fixedUp;
            }
            for (int i = 0; i < n; i++)
                upgrades[i] = Math.Max(0, Math.Min(GameRules.MaxLevelOf((UpgradeType)i), upgrades[i]));
            if (contracts == null) contracts = new List<Contract>();

            if (money < 0) money = 0;
            if (level < 1) level = 1;
            if (level > LevelRules.MaxLevel) level = LevelRules.MaxLevel;
            if (xp < 0 || level >= LevelRules.MaxLevel) xp = 0;

            if (yard == null || !yard.HasShape(GameRules.YardColumns, GameRules.YardRows, GameRules.YardMaxHeight))
                yard = Reshape(yard, GameRules.YardColumns, GameRules.YardRows, GameRules.YardMaxHeight);
            if (ship == null || !ship.HasShape(GameRules.ShipColumns, GameRules.ShipRows, GameRules.ShipMaxHeight))
                ship = Reshape(ship, GameRules.ShipColumns, GameRules.ShipRows, GameRules.ShipMaxHeight);

            // Un navire "à quai" mais vide n'a pas de sens : on le fait repartir.
            if (shipDocked && ship.IsEmpty) shipDocked = false;

            // Horloge reculée : on évite d'attendre un navire plus longtemps que prévu.
            double maxWait = GameRules.ShipInterval(upgrades[(int)UpgradeType.AttenteNavires]);
            if (nextShipUnix > nowUnix + maxWait) nextShipUnix = nowUnix + maxWait;

            // Identifiants uniques : on repart au-dessus du plus grand utilisé.
            int maxId = 0;
            foreach (var g in new[] { yard, ship })
                foreach (var s in g.stacks)
                    foreach (var c in s.items)
                        if (c.id > maxId) maxId = c.id;
            if (nextContainerId <= maxId) nextContainerId = maxId + 1;

            version = CurrentVersion;
        }

        /// <summary>Recrée une grille aux bonnes dimensions en conservant un maximum de conteneurs.</summary>
        static StackGrid Reshape(StackGrid old, int c, int r, int h)
        {
            var g = new StackGrid(c, r, h);
            if (old == null || old.stacks == null) return g;
            // Même disposition au sol (ancienne version moins haute) : on garde chaque pile à sa place.
            if (old.columns == c && old.rows == r && old.stacks.Count == c * r)
            {
                for (int i = 0; i < old.stacks.Count; i++)
                    if (old.stacks[i]?.items != null)
                        foreach (var item in old.stacks[i].items) g.Place(i, item);
                return g;
            }
            int slot = 0;
            foreach (var s in old.stacks)
            {
                if (s == null || s.items == null) continue;
                foreach (var item in s.items)
                {
                    while (slot < g.SlotCount && !g.CanPlace(slot)) slot++;
                    if (slot >= g.SlotCount) return g;
                    g.Place(slot, item);
                }
            }
            return g;
        }
    }
}
