// -----------------------------------------------------------------------------
// Crane Bay - Heure de référence.
// Tous les timers persistants (coffres, navire suivant, turbo) utilisent l'heure
// UTC en secondes Unix : indépendante du fuseau horaire et de Time.time (qui
// repart de zéro à chaque lancement).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Game
{
    public static class TimeUtil
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static double NowUnix => (DateTime.UtcNow - Epoch).TotalSeconds;
    }
}
