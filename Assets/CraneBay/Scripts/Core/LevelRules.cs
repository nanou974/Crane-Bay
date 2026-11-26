// -----------------------------------------------------------------------------
// Crane Bay - Niveaux du joueur (XP).
//
// Chaque conteneur livré rapporte des points d'expérience (XP) selon sa
// couleur (+50 % en train). Quand la barre d'XP est pleine, on passe au niveau
// suivant : prime en argent, et le terminal évolue :
//  - de nouvelles couleurs (plus rentables) apparaissent sur les navires ;
//  - les navires arrivent plus chargés.
// Le seuil d'XP grandit à chaque niveau (progression géométrique douce).
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    public static class LevelRules
    {
        public const int MaxLevel = 30;

        // XP par couleur livrée (même ordre que ContainerColor).
        static readonly int[] ColorXp = { 1, 2, 4, 7, 12 };

        // Niveau à partir duquel chaque couleur peut arriver par navire.
        public static readonly int[] ColorUnlockLevel = { 1, 1, 2, 4, 6 };

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
    }
}
