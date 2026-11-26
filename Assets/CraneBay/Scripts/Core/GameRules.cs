// -----------------------------------------------------------------------------
// Crane Bay - Règles et équilibrage du jeu (valeurs, améliorations, timers).
// Toutes les constantes d'équilibrage sont regroupées ici pour être faciles à
// régler. Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    /// <summary>Les améliorations achetables (l'ordre = index dans la sauvegarde).</summary>
    public enum UpgradeType
    {
        VitesseGrue = 0,
        AttenteNavires = 1,
        AttenteVehicules = 2,
        AgrandirParc = 3,
        DeuxiemeGrue = 4,
    }

    public static class GameRules
    {
        public const int UpgradeCount = 5;

        // --- Parc de stockage --------------------------------------------------
        // La grille de stockage a toujours 5 x 4 emplacements et 4 niveaux de
        // capacité, mais une partie est verrouillée au début et se débloque avec
        // l'amélioration "Agrandir le parc".
        public const int YardColumns = 5;
        public const int YardRows = 4;
        public const int YardMaxHeight = 4;

        /// <summary>Rangées utilisables : 3 au départ, 4 après le 1er agrandissement.</summary>
        public static int YardRowsUnlocked(int parcLevel) => parcLevel >= 1 ? 4 : 3;

        /// <summary>Hauteur de pile autorisée : 3 au départ, 4 après le 2e agrandissement.</summary>
        public static int YardHeightLimit(int parcLevel) => parcLevel >= 2 ? 4 : 3;

        /// <summary>Rangée électrifiée (prises pour frigorifiques) : celle côté quai.</summary>
        public const int PoweredRow = 0;

        // --- Cales du navire ---------------------------------------------------
        public const int ShipColumns = 4;
        public const int ShipRows = 2;
        public const int ShipMaxHeight = 2;
        public const int ShipMinCargo = 6;
        public const int ShipMaxCargo = 12;

        // --- Véhicules ---------------------------------------------------------
        public const int TruckBays = 2;      // deux places de camion côté route
        public const int TrainWagons = 3;    // un train = 3 wagons à remplir
        public const int TrainEvery = 3;     // 1 arrivée sur 3 est un train
        public const float TrainBonus = 1.5f; // le train paie 50 % de plus par conteneur
        public const int UpcomingCount = 3;  // commandes annoncées à l'avance

        // --- Bonus de rapidité -------------------------------------------------
        public const double FastDeliverySeconds = 10;
        public const float FastBonus = 1.2f;

        // --- Valeur des conteneurs ($) -----------------------------------------
        public static readonly int[] ColorValues = { 10, 25, 50, 100, 250 };

        // Probabilités relatives d'apparition (blanc fréquent, doré rare).
        public static readonly int[] ColorWeights = { 40, 25, 18, 12, 5 };

        public static readonly string[] ColorNames = { "Blanc", "Bleu", "Vert", "Rouge", "Doré" };

        public static int ColorCount => ColorValues.Length;

        public const float SmallValueFactor = 0.6f;   // un 20 pieds vaut 60 % d'un 40 pieds
        public const float ReeferValueFactor = 1.5f;  // marchandise réfrigérée : mieux payée
        public const float HazardValueFactor = 1.4f;  // matières dangereuses : prime de risque
        public const float ReeferSpoilSeconds = 90f;  // temps débranché pour atteindre la valeur minimale
        public const float ReeferMinFactor = 0.3f;

        public static int ValueOf(ContainerColor c) => ColorValues[(int)c];

        /// <summary>État de fraîcheur d'un frigorifique (1 = intact, 0,3 = très abîmé).</summary>
        public static float Freshness(ContainerInfo c)
        {
            if (c == null || !c.reefer) return 1f;
            float f = 1f - c.unplugged / ReeferSpoilSeconds;
            return Math.Max(ReeferMinFactor, Math.Min(1f, f));
        }

        /// <summary>Valeur actuelle d'un conteneur (taille, type, fraîcheur), sans bonus de livraison.</summary>
        public static int ValueOf(ContainerInfo c)
        {
            double v = ColorValues[c.color];
            if (c.IsSmall) v *= SmallValueFactor;
            if (c.reefer) v *= ReeferValueFactor * Freshness(c);
            if (c.hazard) v *= HazardValueFactor;
            return Math.Max(1, (int)Math.Round(v));
        }

        /// <summary>Gain pour un conteneur livré (bonus train et rapidité éventuels).</summary>
        public static int Payout(ContainerInfo c, bool train, bool fast = false)
        {
            double v = ValueOf(c);
            if (train) v *= TrainBonus;
            if (fast) v *= FastBonus;
            return (int)Math.Round(v);
        }

        /// <summary>Gain "de base" d'une couleur (affiché sur les demandes des véhicules).</summary>
        public static int Payout(ContainerColor c, bool train)
        {
            int v = ValueOf(c);
            return train ? (int)Math.Round(v * TrainBonus) : v;
        }

        // --- Améliorations -----------------------------------------------------
        public const int UpgradeMaxLevel = 10; // niveau max des 3 améliorations "à niveaux"
        static readonly int[] MaxLevels = { 10, 10, 10, 2, 1 };
        static readonly int[] UpgradeBaseCost = { 100, 150, 120, 900, 2500 };
        static readonly double[] UpgradeCostGrowth = { 1.6, 1.65, 1.6, 2.8, 1.0 };

        public static readonly string[] UpgradeNames = { "Vitesse grue", "Attente navires", "Attente camions", "Agrandir le parc", "2e grue" };

        public static int MaxLevelOf(UpgradeType t) => MaxLevels[(int)t];

        public static bool IsMaxed(UpgradeType t, int level) => level >= MaxLevelOf(t);

        /// <summary>
        /// Les prix augmentent aussi avec le niveau du joueur (+12 % par niveau,
        /// composé) : les gains grandissent avec les niveaux, les prix suivent.
        /// </summary>
        public const double PlayerLevelCostGrowth = 1.12;

        public static double PlayerLevelCostFactor(int playerLevel) =>
            Math.Pow(PlayerLevelCostGrowth, Math.Max(0, playerLevel - 1));

        /// <summary>
        /// Coût pour passer une amélioration du niveau "level" au suivant :
        /// croissance géométrique selon le niveau de l'amélioration, multipliée
        /// par le facteur lié au niveau du joueur.
        /// </summary>
        public static int UpgradeCost(UpgradeType type, int level, int playerLevel = 1)
        {
            int i = (int)type;
            double cost = UpgradeBaseCost[i] * Math.Pow(UpgradeCostGrowth[i], level) * PlayerLevelCostFactor(playerLevel);
            // Arrondi "propre" à 5 $ près pour l'affichage.
            return (int)(Math.Round(cost / 5.0) * 5.0);
        }

        /// <summary>Multiplicateur de vitesse de la grue (+20 % par niveau).</summary>
        public static float CraneSpeedMultiplier(int level) => 1f + 0.2f * level;

        /// <summary>Attente entre deux navires, en secondes (-15 % par niveau).</summary>
        public static float ShipInterval(int level) => 45f * (float)Math.Pow(0.85, level);

        /// <summary>Attente entre deux camions/trains, en secondes (-15 % par niveau).</summary>
        public static float VehicleInterval(int level) => 16f * (float)Math.Pow(0.85, level);

        /// <summary>Texte court décrivant l'effet d'une amélioration à un niveau donné.</summary>
        public static string EffectLabel(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.VitesseGrue: return "x" + CraneSpeedMultiplier(level).ToString("0.0");
                case UpgradeType.AttenteNavires: return ShipInterval(level).ToString("0") + " s";
                case UpgradeType.AttenteVehicules: return VehicleInterval(level).ToString("0.0") + " s";
                case UpgradeType.AgrandirParc: return YardColumns + "x" + YardRowsUnlocked(level) + " h" + YardHeightLimit(level);
                default: return level >= 1 ? "Oui" : "Non";
            }
        }

        // --- Coffres -----------------------------------------------------------
        public const double DailyChestCooldown = 24 * 3600;   // 24 h
        public const double QuickChestCooldown = 20 * 60;     // 20 min
        public const double TurboDurationQuick = 120;         // 2 min de turbo
        public const double TurboDurationDaily = 300;         // 5 min de turbo
        public const float TurboMultiplier = 2f;              // turbo = grue 2x plus rapide
    }
}
