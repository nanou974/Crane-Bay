// -----------------------------------------------------------------------------
// Crane Bay - Niveaux du joueur (XP) et ce qu'ils débloquent.
//
// Chaque conteneur livré rapporte des points d'expérience (XP) selon sa
// couleur (+50 % en train). Quand la barre d'XP est pleine, on passe au niveau
// suivant : prime en argent, remise à zéro du terminal, et le jeu s'enrichit :
//  - nouvelles couleurs (vert niv. 2, rouge niv. 4, doré niv. 6) ;
//  - nouveaux types : 20 pieds (niv. 2), frigorifiques (niv. 3),
//    matières dangereuses (niv. 5) ;
//  - navires plus chargés (+1 conteneur tous les 3 niveaux) ;
//  - nouveau décor : port tropical (niv. 10), port polaire (niv. 20).
// Le seuil d'XP grandit de 30 % à chaque niveau.
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    public enum PortTheme { Tempere = 0, Tropical = 1, Polaire = 2 }

    public static class LevelRules
    {
        public const int MaxLevel = 30;

        // XP par couleur livrée (même ordre que ContainerColor).
        static readonly int[] ColorXp = { 1, 2, 4, 7, 12 };

        // Niveau à partir duquel chaque couleur peut arriver par navire.
        public static readonly int[] ColorUnlockLevel = { 1, 1, 2, 4, 6 };

        public const int SmallContainersLevel = 2;
        public const int ReeferLevel = 3;
        public const int HazardLevel = 5;
        public const int TropicalLevel = 10;
        public const int PolarLevel = 20;

        // Proportions des types spéciaux (une fois débloqués).
        public const double SmallChance = 0.30;
        public const double ReeferChance = 0.12;
        public const double HazardChance = 0.07;

        public static int XpFor(ContainerColor c, bool train)
        {
            int xp = ColorXp[(int)c];
            return train ? (int)Math.Ceiling(xp * GameRules.TrainBonus) : xp;
        }

        /// <summary>XP nécessaires pour passer du niveau "level" au suivant (0 au niveau max).</summary>
        public static int XpToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return (int)Math.Round(12 * Math.Pow(1.3, level - 1));
        }

        /// <summary>Prime en argent reçue en atteignant "newLevel".</summary>
        public static int LevelUpReward(int newLevel) => 50 * newLevel;

        /// <summary>Conteneurs supplémentaires par navire : +1 tous les 3 niveaux.</summary>
        public static int ExtraCargo(int level) => Math.Max(0, (level - 1) / 3);

        public static bool IsColorUnlocked(ContainerColor c, int level) => level >= ColorUnlockLevel[(int)c];

        /// <summary>Couleur débloquée à exactement ce niveau (ou -1).</summary>
        public static int ColorUnlockedAt(int level)
        {
            for (int i = 0; i < ColorUnlockLevel.Length; i++)
                if (ColorUnlockLevel[i] == level && level > 1) return i;
            return -1;
        }

        public static PortTheme ThemeFor(int level) =>
            level >= PolarLevel ? PortTheme.Polaire : level >= TropicalLevel ? PortTheme.Tropical : PortTheme.Tempere;

        public static readonly string[] ThemeNames = { "Port tempéré", "Port tropical", "Port polaire" };

        /// <summary>Texte des nouveautés débloquées à ce niveau (vide si rien de neuf).</summary>
        public static string UnlocksAt(int level)
        {
            var parts = new System.Collections.Generic.List<string>();
            int color = ColorUnlockedAt(level);
            if (color >= 0) parts.Add("conteneurs " + GameRules.ColorNames[color].ToLowerInvariant() + "s");
            if (level == SmallContainersLevel) parts.Add("conteneurs 20 pieds");
            if (level == ReeferLevel) parts.Add("frigorifiques (à brancher rangée du haut)");
            if (level == HazardLevel) parts.Add("matières dangereuses (jamais côte à côte)");
            if (level == TropicalLevel || level == PolarLevel) parts.Add(ThemeNames[(int)ThemeFor(level)]);
            if (parts.Count == 0 && ExtraCargo(level) > ExtraCargo(level - 1)) parts.Add("navires plus chargés");
            return string.Join(", ", parts);
        }

        /// <summary>Tire une couleur parmi celles débloquées, selon les poids de GameRules.</summary>
        public static ContainerColor RollColor(Random rng, int level)
        {
            var weights = GameRules.ColorWeights;
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
                if (IsColorUnlocked((ContainerColor)i, level)) total += weights[i];
            int r = rng.Next(total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (!IsColorUnlocked((ContainerColor)i, level)) continue;
                if (r < weights[i]) return (ContainerColor)i;
                r -= weights[i];
            }
            return ContainerColor.Blanc;
        }

        /// <summary>Crée un conteneur aléatoire adapté au niveau (couleur, taille, type).</summary>
        public static ContainerInfo RollContainer(Random rng, int level, int id)
        {
            var color = RollColor(rng, level);
            int size = level >= SmallContainersLevel && rng.NextDouble() < SmallChance ? 20 : 40;
            bool reefer = level >= ReeferLevel && rng.NextDouble() < ReeferChance;
            bool hazard = !reefer && level >= HazardLevel && rng.NextDouble() < HazardChance;
            return new ContainerInfo(id, color, size, reefer, hazard);
        }
    }
}
