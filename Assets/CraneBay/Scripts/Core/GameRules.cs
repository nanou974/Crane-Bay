// -----------------------------------------------------------------------------
// Crane Bay - Règles et équilibrage du jeu (valeurs, améliorations, timers).
// Toutes les constantes d'équilibrage sont regroupées ici pour être faciles à
// régler. Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    /// <summary>Les trois améliorations achetables.</summary>
    public enum UpgradeType
    {
        VitesseGrue = 0,
        AttenteNavires = 1,
        AttenteVehicules = 2,
    }

    public static class GameRules
    {
        // --- Parc de stockage --------------------------------------------------
        public const int YardColumns = 5;
        public const int YardRows = 4;
        public const int YardMaxHeight = 3;

        // --- Cales du navire ---------------------------------------------------
        public const int ShipColumns = 4;
        public const int ShipRows = 2;
        public const int ShipMaxHeight = 2;
        public const int ShipMinCargo = 6;
        public const int ShipMaxCargo = 12;

        // --- Véhicules ---------------------------------------------------------
        public const int TruckBays = 2;      // deux places de camion côté route
        public const int TrainWagons = 3;    // un train = 3 wagons à remplir
        public const int TrainEvery = 3;     // 1 arrivée sur 3 est un train (si la voie est libre)
        public const float TrainBonus = 1.5f; // le train paie 50 % de plus par conteneur

        // --- Valeur des conteneurs ($) -----------------------------------------
        public static readonly int[] ColorValues = { 10, 25, 50, 100, 250 };

        // Probabilités relatives d'apparition (blanc fréquent, doré rare).
        public static readonly int[] ColorWeights = { 40, 25, 18, 12, 5 };

        public static readonly string[] ColorNames = { "Blanc", "Bleu", "Vert", "Rouge", "Doré" };

        public static int ColorCount => ColorValues.Length;

        public static int ValueOf(ContainerColor c) => ColorValues[(int)c];

        /// <summary>Gain pour un conteneur livré (bonus si c'est un train).</summary>
        public static int Payout(ContainerColor c, bool train)
        {
            int v = ValueOf(c);
            return train ? (int)Math.Round(v * TrainBonus) : v;
        }

        /// <summary>Tire une couleur au hasard selon les poids.</summary>
        public static ContainerColor RollColor(Random rng)
        {
            int total = 0;
            foreach (var w in ColorWeights) total += w;
            int r = rng.Next(total);
            for (int i = 0; i < ColorWeights.Length; i++)
            {
                if (r < ColorWeights[i]) return (ContainerColor)i;
                r -= ColorWeights[i];
            }
            return ContainerColor.Blanc;
        }

        // --- Améliorations -----------------------------------------------------
        public const int UpgradeMaxLevel = 10;
        static readonly int[] UpgradeBaseCost = { 100, 150, 120 };
        static readonly double[] UpgradeCostGrowth = { 1.6, 1.65, 1.6 };

        public static readonly string[] UpgradeNames = { "Vitesse grue", "Attente navires", "Attente camions/trains" };

        public static bool IsMaxed(int level) => level >= UpgradeMaxLevel;

        /// <summary>Coût pour passer du niveau "level" au niveau suivant (croissance géométrique).</summary>
        public static int UpgradeCost(UpgradeType type, int level)
        {
            int i = (int)type;
            double cost = UpgradeBaseCost[i] * Math.Pow(UpgradeCostGrowth[i], level);
            // Arrondi "propre" à 5 $ près pour l'affichage.
            return (int)(Math.Round(cost / 5.0) * 5.0);
        }

        /// <summary>Multiplicateur de vitesse de la grue (+20 % par niveau).</summary>
        public static float CraneSpeedMultiplier(int level) => 1f + 0.2f * level;

        /// <summary>Attente entre deux navires, en secondes (-15 % par niveau).</summary>
        public static float ShipInterval(int level) => 45f * (float)Math.Pow(0.85, level);

        /// <summary>Attente entre deux camions/trains, en secondes (-15 % par niveau).</summary>
        public static float VehicleInterval(int level) => 16f * (float)Math.Pow(0.85, level);

        /// <summary>Texte court décrivant l'effet actuel d'une amélioration.</summary>
        public static string EffectLabel(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.VitesseGrue: return "x" + CraneSpeedMultiplier(level).ToString("0.0");
                case UpgradeType.AttenteNavires: return ShipInterval(level).ToString("0") + " s";
                default: return VehicleInterval(level).ToString("0.0") + " s";
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
