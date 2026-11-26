// -----------------------------------------------------------------------------
// Crane Bay - Logique des coffres (quotidien et toutes les 20 minutes).
//
// Principe : on n'enregistre PAS un compte à rebours, mais l'heure (UTC, en
// secondes Unix) de la dernière ouverture. Le temps restant est recalculé à
// partir de l'horloge à chaque instant. Le timer reste donc exact même si le
// jeu est fermé ou le téléphone redémarré, et le coffre se régénère hors ligne.
//
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    public enum ChestKind { Quotidien = 0, VingtMinutes = 1 }

    public enum RewardKind { Argent = 0, Turbo = 1 }

    /// <summary>Contenu d'un coffre.</summary>
    public struct ChestReward
    {
        public RewardKind kind;
        public int money;          // argent gagné (toujours >= 0)
        public double turboSeconds; // durée du turbo de grue (0 si aucun)
    }

    public static class ChestLogic
    {
        public static double Cooldown(ChestKind kind) =>
            kind == ChestKind.Quotidien ? GameRules.DailyChestCooldown : GameRules.QuickChestCooldown;

        /// <summary>
        /// Secondes restantes avant que le coffre soit prêt (0 = prêt).
        /// lastOpenUnix = 0 signifie "jamais ouvert" : le coffre est prêt.
        /// Si l'horloge du téléphone a reculé (now &lt; last), le temps restant est
        /// borné à la durée du cooldown pour ne jamais bloquer le joueur plus
        /// longtemps que prévu.
        /// </summary>
        public static double Remaining(ChestKind kind, double lastOpenUnix, double nowUnix)
        {
            if (lastOpenUnix <= 0) return 0;
            double cd = Cooldown(kind);
            double rem = lastOpenUnix + cd - nowUnix;
            if (rem < 0) rem = 0;
            if (rem > cd) rem = cd;
            return rem;
        }

        public static bool IsReady(ChestKind kind, double lastOpenUnix, double nowUnix) =>
            Remaining(kind, lastOpenUnix, nowUnix) <= 0;

        /// <summary>
        /// Tire le contenu d'un coffre. "progress" = somme des niveaux
        /// d'amélioration : les récompenses grandissent avec la progression.
        /// </summary>
        public static ChestReward Roll(ChestKind kind, int progress, Random rng)
        {
            var reward = new ChestReward();
            double scale = 1.0 + 0.3 * progress;
            if (kind == ChestKind.Quotidien)
            {
                // Coffre du jour : toujours de l'argent, parfois un turbo en plus.
                reward.money = RoundTo5(rng.Next(250, 401) * scale);
                if (rng.NextDouble() < 0.35)
                {
                    reward.kind = RewardKind.Turbo;
                    reward.turboSeconds = GameRules.TurboDurationDaily;
                }
                else reward.kind = RewardKind.Argent;
            }
            else
            {
                // Coffre 20 min : de l'argent OU un turbo de 2 minutes.
                if (rng.NextDouble() < 0.25)
                {
                    reward.kind = RewardKind.Turbo;
                    reward.turboSeconds = GameRules.TurboDurationQuick;
                    reward.money = 0;
                }
                else
                {
                    reward.kind = RewardKind.Argent;
                    reward.money = RoundTo5(rng.Next(40, 81) * scale);
                }
            }
            return reward;
        }

        static int RoundTo5(double v) => (int)(Math.Round(v / 5.0) * 5.0);

        /// <summary>Formate un temps restant : "12:34" ou "5 h 03".</summary>
        public static string Format(double seconds)
        {
            if (seconds <= 0) return "Prêt !";
            int s = (int)Math.Ceiling(seconds);
            int h = s / 3600, m = (s % 3600) / 60, sec = s % 60;
            if (h > 0) return h + " h " + m.ToString("00");
            return m + ":" + sec.ToString("00");
        }
    }
}
