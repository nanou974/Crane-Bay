// -----------------------------------------------------------------------------
// Crane Bay - Météo et cycle jour/nuit.
// Calculés à partir de l'heure : aucune donnée à sauvegarder, et le résultat
// est le même pour tout le monde au même moment.
//  - Cycle jour/nuit de 10 minutes (la nuit dure environ 3 minutes).
//  - Météo changeante toutes les 3 minutes : beau temps, vent (grue -15 %),
//    pluie (grue -5 %).
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    public enum Weather { BeauTemps = 0, Vent = 1, Pluie = 2 }

    public static class Ambience
    {
        public const double DayCycleSeconds = 600;
        public const double WeatherBlockSeconds = 180;

        public static readonly string[] WeatherNames = { "Beau temps", "Vent : grue -15 %", "Pluie : grue -5 %" };

        /// <summary>Intensité de la nuit, 0 (plein jour) à 1 (nuit noire), avec transitions douces.</summary>
        public static float NightAmount(double nowUnix)
        {
            double phase = (nowUnix % DayCycleSeconds) / DayCycleSeconds; // 0..1
            // Nuit centrée sur 0,85 du cycle, largeur ~0,3, bords lissés.
            double d = Math.Abs(phase - 0.85);
            if (d > 0.5) d = 1 - d;
            double t = 1 - (d - 0.1) / 0.1; // 1 au cœur, 0 au-delà de 0,2
            t = Math.Max(0, Math.Min(1, t));
            return (float)(t * t * (3 - 2 * t));
        }

        /// <summary>Météo du moment (pseudo-aléatoire par tranche de 3 minutes).</summary>
        public static Weather WeatherAt(double nowUnix)
        {
            long block = (long)Math.Floor(nowUnix / WeatherBlockSeconds);
            // Petit hachage entier déterministe.
            ulong h = (ulong)block * 2654435761UL;
            h ^= h >> 13;
            int r = (int)(h % 100);
            if (r < 60) return Weather.BeauTemps;
            if (r < 85) return Weather.Vent;
            return Weather.Pluie;
        }

        public static float CraneFactor(Weather w) => w == Weather.Vent ? 0.85f : w == Weather.Pluie ? 0.95f : 1f;
    }
}
