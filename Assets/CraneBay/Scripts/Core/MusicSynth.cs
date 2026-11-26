// -----------------------------------------------------------------------------
// Crane Bay - Musique d'ambiance générée par code (style "chill" et enjoué).
//
// Aucun fichier audio, donc aucune question de droits : la boucle est
// synthétisée au lancement, toujours identique (générateur pseudo-aléatoire
// à graine fixe). Elle dure 8 mesures à 88 BPM (environ 22 s) et boucle sans
// coupure : les fins de notes qui dépassent la boucle sont repliées au début.
//
// Couches :
//  - accords de piano électrique doux (Do maj7, La m7, Fa maj7, Sol 7) ;
//  - basse ronde sur les fondamentales ;
//  - batterie légère et swinguée (grosse caisse, claquement, charleston) ;
//  - petite mélodie de "marimba" sur la gamme pentatonique.
//
// Code pur C# (System.Math) : peut tourner hors du thread principal et être
// testé sans Unity.
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    public static class MusicSynth
    {
        public const float Bpm = 88f;
        public const int Bars = 8;
        public const int BeatsPerBar = 4;
        const float Swing = 0.62f;     // position de la croche "et" dans le temps (0.5 = droit)
        const float Peak = 0.8f;       // niveau maximal après normalisation

        // Accords par mesure (notes MIDI) et basse correspondante.
        static readonly int[][] Chords =
        {
            new[] { 60, 64, 67, 71 }, // Do maj7
            new[] { 57, 60, 64, 67 }, // La m7
            new[] { 53, 57, 60, 64 }, // Fa maj7
            new[] { 55, 59, 62, 65 }, // Sol 7
        };
        static readonly int[] BassRoots = { 36, 33, 41, 43 };
        static readonly int[] Pentatonic = { 72, 74, 76, 79, 81, 84 };

        public static float BeatSeconds => 60f / Bpm;
        public static float LoopSeconds => Bars * BeatsPerBar * BeatSeconds;

        /// <summary>Nombre d'échantillons de la boucle à cette fréquence.</summary>
        public static int SampleCount(int rate) => (int)Math.Round(LoopSeconds * rate);

        /// <summary>Calcule la boucle complète (mono, valeurs entre -Peak et +Peak).</summary>
        public static float[] Render(int rate)
        {
            var buf = new float[SampleCount(rate)];
            var rng = new Random(7);
            float beat = BeatSeconds;

            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = bar % Chords.Length;
                float t0 = bar * BeatsPerBar * beat;

                // Piano électrique : un accord sur le 1er temps, un rappel sur le "et" du 2e.
                foreach (var n in Chords[chord])
                {
                    Note(buf, rate, t0, 1.6f * beat, Midi(n), 0.11f, Timbre.EPiano);
                    Note(buf, rate, t0 + (1f + Swing) * beat, 1.8f * beat, Midi(n), 0.08f, Timbre.EPiano);
                }

                // Basse : fondamentale sur le 1, rappel sur le 3, petite note de passage sur le "et" du 4.
                float root = Midi(BassRoots[chord]);
                Note(buf, rate, t0, 1.6f * beat, root, 0.32f, Timbre.Bass);
                Note(buf, rate, t0 + 2f * beat, 0.9f * beat, root, 0.26f, Timbre.Bass);
                Note(buf, rate, t0 + (3f + Swing) * beat, 0.45f * beat, root * 2f, 0.16f, Timbre.Bass);

                // Batterie.
                Hit(buf, rate, t0, Drum.Kick, 0.5f, rng);
                Hit(buf, rate, t0 + 2f * beat, Drum.Kick, 0.45f, rng);
                if (bar % 2 == 1) Hit(buf, rate, t0 + (2f + Swing) * beat, Drum.Kick, 0.3f, rng);
                Hit(buf, rate, t0 + beat, Drum.Snap, 0.16f, rng);
                Hit(buf, rate, t0 + 3f * beat, Drum.Snap, 0.16f, rng);
                for (int b = 0; b < BeatsPerBar; b++)
                {
                    Hit(buf, rate, t0 + b * beat, Drum.Hat, 0.035f, rng);
                    Hit(buf, rate, t0 + (b + Swing) * beat, Drum.Hat, 0.06f, rng);
                }
            }

            // Mélodie : un motif de 4 mesures, rejoué avec une fin différente.
            var motif = MakeMotif(new Random(3));
            var ending = MakeMotif(new Random(11));
            for (int half = 0; half < 2; half++)
            {
                for (int slot = 0; slot < 4 * BeatsPerBar * 2; slot++)
                {
                    bool lastBar = slot >= 3 * BeatsPerBar * 2;
                    int note = half == 1 && lastBar ? ending[slot] : motif[slot];
                    if (note < 0) continue;
                    int b = slot / 2;
                    float t = (half * 4 * BeatsPerBar + b + (slot % 2 == 1 ? Swing : 0f)) * beat;
                    Note(buf, rate, t, 0.9f * beat, Midi(note), 0.1f, Timbre.Marimba);
                }
            }

            LowPassAndNormalize(buf, rate);
            return buf;
        }

        /// <summary>Motif mélodique : une croche sur trois environ, par petits pas dans la gamme.</summary>
        static int[] MakeMotif(Random rng)
        {
            var notes = new int[4 * BeatsPerBar * 2];
            int idx = 2;
            for (int i = 0; i < notes.Length; i++)
            {
                bool strong = i % 4 == 0;
                if (rng.NextDouble() > (strong ? 0.6 : 0.3)) { notes[i] = -1; continue; }
                idx = Math.Max(0, Math.Min(Pentatonic.Length - 1, idx + rng.Next(-2, 3)));
                notes[i] = Pentatonic[idx];
            }
            return notes;
        }

        enum Timbre { EPiano, Bass, Marimba }
        enum Drum { Kick, Snap, Hat }

        static float Midi(int n) => 440f * (float)Math.Pow(2.0, (n - 69) / 12.0);

        /// <summary>Ajoute une note ; ce qui dépasse la fin de la boucle revient au début.</summary>
        static void Note(float[] buf, int rate, float start, float length, float freq, float amp, Timbre timbre)
        {
            int s0 = (int)(start * rate);
            float tail = timbre == Timbre.Bass ? 0.08f : 0.35f;
            int count = (int)((length + tail) * rate);
            double w = 2.0 * Math.PI * freq;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                double env, v;
                switch (timbre)
                {
                    case Timbre.EPiano:
                        env = Math.Min(1.0, t / 0.012) * Math.Exp(-2.0 * t);
                        // Léger trémolo et harmonique qui s'éteint vite : son "Rhodes" adouci.
                        v = Math.Sin(w * t) + 0.3 * Math.Sin(2 * w * t) * Math.Exp(-6 * t);
                        v *= 1.0 + 0.08 * Math.Sin(2 * Math.PI * 4.5 * t);
                        break;
                    case Timbre.Bass:
                        env = Math.Min(1.0, t / 0.006) * Math.Exp(-1.5 * t);
                        v = Math.Sin(w * t) + 0.2 * Math.Sin(2 * w * t);
                        break;
                    default: // Marimba
                        env = Math.Min(1.0, t / 0.003) * Math.Exp(-5.5 * t);
                        v = Math.Sin(w * t) + 0.25 * Math.Sin(4 * w * t) * Math.Exp(-14 * t);
                        break;
                }
                // Relâchement doux après la durée de la note.
                if (t > length) env *= Math.Max(0.0, 1.0 - (t - length) / tail);
                buf[(s0 + i) % buf.Length] += (float)(v * env * amp);
            }
        }

        static void Hit(float[] buf, int rate, float start, Drum drum, float amp, Random rng)
        {
            int s0 = (int)(start * rate);
            float len = drum == Drum.Kick ? 0.35f : drum == Drum.Snap ? 0.18f : 0.05f;
            int count = (int)(len * rate);
            double phase = 0, prevNoise = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                double v;
                switch (drum)
                {
                    case Drum.Kick:
                        // Sinus dont la hauteur chute de 120 à 45 Hz.
                        double f = 45 + 75 * Math.Exp(-30 * t);
                        phase += 2 * Math.PI * f / rate;
                        v = Math.Sin(phase) * Math.Exp(-9 * t);
                        break;
                    case Drum.Snap:
                        v = ((rng.NextDouble() * 2 - 1) * 0.8 + Math.Sin(2 * Math.PI * 210 * t) * 0.5) * Math.Exp(-26 * t);
                        break;
                    default: // Charleston : bruit "aigu" (différence de deux tirages successifs).
                        double n = rng.NextDouble() * 2 - 1;
                        v = (n - prevNoise) * 0.5 * Math.Exp(-70 * t);
                        prevNoise = n;
                        break;
                }
                buf[(s0 + i) % buf.Length] += (float)(v * amp);
            }
        }

        /// <summary>Adoucit les aigus (son "lo-fi") puis ramène le niveau maximal à Peak.</summary>
        static void LowPassAndNormalize(float[] buf, int rate)
        {
            double cutoff = 4200;
            double a = 1 - Math.Exp(-2 * Math.PI * cutoff / rate);
            // Deux passages : le second démarre avec l'état de fin du premier, pour un raccord propre.
            double y = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < buf.Length; i++)
                {
                    y += a * (buf[i] - y);
                    if (pass == 1) buf[i] = (float)y;
                }
            float max = 0f;
            foreach (var s in buf) max = Math.Max(max, Math.Abs(s));
            if (max <= 0f) return;
            float k = Peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }
    }
}
