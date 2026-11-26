// -----------------------------------------------------------------------------
// Crane Bay - Contrats du jour.
// Trois objectifs renouvelés chaque jour (minuit UTC), du type
// "Livrer 5 conteneurs rouges en train". Un contrat rempli verse aussitôt sa
// récompense (argent + XP). La progression est sauvegardée.
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    [Serializable]
    public class Contract
    {
        public int color = -1;      // -1 = n'importe quelle couleur
        public int vehicle = -1;    // -1 = n'importe quel véhicule, 0 = camion, 1 = train
        public int special = 0;     // 0 = aucun, 1 = frigorifique, 2 = dangereux, 3 = livraison rapide
        public int target;
        public int progress;
        public int rewardMoney;
        public int rewardXp;
        public bool done;

        public string Describe()
        {
            string what;
            switch (special)
            {
                case 1: what = target + " frigorifique" + (target > 1 ? "s" : ""); break;
                case 2: what = target + " conteneur" + (target > 1 ? "s" : "") + " dangereux"; break;
                case 3: what = target + " livraison" + (target > 1 ? "s" : "") + " rapide" + (target > 1 ? "s" : ""); break;
                default:
                    what = target + " conteneur" + (target > 1 ? "s" : "");
                    if (color >= 0)
                    {
                        string name = GameRules.ColorNames[color].ToLowerInvariant();
                        what += " " + name + (target > 1 ? "s" : "");
                    }
                    break;
            }
            string by = vehicle == 1 ? " en train" : vehicle == 0 ? " en camion" : "";
            return "Livrer " + what + by;
        }
    }

    public static class ContractRules
    {
        public const int Count = 3;

        /// <summary>Numéro du jour (UTC) : change à minuit UTC.</summary>
        public static long DayIndex(double nowUnix) => (long)Math.Floor(nowUnix / 86400.0);

        /// <summary>Génère les contrats du jour, adaptés au niveau du joueur.</summary>
        public static List<Contract> Generate(Random rng, int level)
        {
            var list = new List<Contract>();
            for (int i = 0; i < Count; i++)
            {
                var c = new Contract();
                int kind = rng.Next(level >= LevelRules.ReeferLevel ? 5 : 3);
                switch (kind)
                {
                    case 0: // n'importe quoi, beaucoup
                        c.target = 10 + 2 * level;
                        break;
                    case 1: // une couleur précise
                        c.color = (int)LevelRules.RollColor(rng, level);
                        c.target = Math.Max(2, 8 - c.color * 2 + level / 3);
                        break;
                    case 2: // en train
                        c.vehicle = 1;
                        c.target = 6 + level;
                        break;
                    case 3: // livraisons rapides
                        c.special = 3;
                        c.target = 4 + level / 2;
                        break;
                    default: // frigorifiques (ou dangereux plus tard)
                        c.special = level >= LevelRules.HazardLevel && rng.Next(2) == 0 ? 2 : 1;
                        c.target = 2 + level / 4;
                        break;
                }
                double scale = GameRules.PlayerLevelCostFactor(level);
                c.rewardMoney = (int)(Math.Round(c.target * 15 * scale / 5.0) * 5);
                c.rewardXp = Math.Max(3, LevelRules.XpToNext(level) / 5);
                list.Add(c);
            }
            return list;
        }

        /// <summary>La livraison compte-t-elle pour ce contrat ?</summary>
        public static bool Matches(Contract c, ContainerInfo delivered, bool train, bool fast)
        {
            if (c.done) return false;
            if (c.vehicle == 1 && !train) return false;
            if (c.vehicle == 0 && train) return false;
            if (c.color >= 0 && delivered.color != c.color) return false;
            if (c.special == 1 && !delivered.reefer) return false;
            if (c.special == 2 && !delivered.hazard) return false;
            if (c.special == 3 && !fast) return false;
            return true;
        }
    }
}
