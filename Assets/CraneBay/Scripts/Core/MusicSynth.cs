// -----------------------------------------------------------------------------
// Crane Bay - Musique d'ambiance générée par code.
//
// Aucun fichier audio, donc aucune question de droits : la boucle est
// synthétisée au lancement, toujours identique (générateur pseudo-aléatoire
// à graine fixe). Elle dure 8 mesures et boucle sans coupure : les fins de
// notes qui dépassent la boucle sont repliées au début.
//
// Plusieurs styles sont disponibles (voir MusicStyle) ; chacun définit le
// tempo, les accords, les rythmes et les "instruments" :
//  - Chill      : piano électrique, basse ronde, batterie swinguée, marimba ;
//  - Tropical   : ukulélé, steel drum, basse rebondissante, shaker ;
//  - Bossa      : accords syncopés, basse 1-5, clave, flûte ;
//  - Retro8Bit  : arpèges en signaux carrés, basse triangle, batterie bruitée.
//
// Code pur C# (System.Math) : peut tourner hors du thread principal et être
// testé sans Unity.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    public enum MusicStyle { Chill, Tropical, Bossa, Retro8Bit }

    public static class MusicSynth
    {
        public const int Bars = 8;
        public const int BeatsPerBar = 4;
        const float Peak = 0.8f; // niveau maximal après normalisation

        /// <summary>Style joué dans le jeu.</summary>
        public const MusicStyle DefaultStyle = MusicStyle.Chill;

        public static readonly string[] StyleNames = { "Chill", "Tropical", "Bossa", "Rétro 8-bit" };

        enum Timbre { EPiano, Bass, Marimba, Steel, Pluck, Square, Triangle, Flute, Pad }
        enum Drum { Kick, Snap, Hat, Shaker, Rim, NoiseKick, NoiseSnare }

        struct Hit
        {
            public float beat, dur, amp, mul; // position dans la mesure, durée (temps), volume, octave (x2 = +1)
            public Hit(float beat, float dur, float amp, float mul = 1f) { this.beat = beat; this.dur = dur; this.amp = amp; this.mul = mul; }
        }

        struct DrumHit
        {
            public float beat, amp; public Drum drum; public int everyOtherBar; // 0 = chaque mesure, 1 = mesures impaires
            public DrumHit(float beat, Drum drum, float amp, int everyOtherBar = 0) { this.beat = beat; this.drum = drum; this.amp = amp; this.everyOtherBar = everyOtherBar; }
        }

        class Style
        {
            public float bpm, swing = 0.5f, cutoff = 5000f;
            public int[][] chords;          // 4 accords (MIDI), un par mesure, répétés
            public int[] bass;              // fondamentales (MIDI) correspondantes
            public Hit[] chordHits;         // rythme des accords
            public Timbre chordTimbre;
            public float arpAmp;            // > 0 : arpège en doubles croches au lieu d'accords plaqués
            public Hit[] bassHits;
            public Timbre bassTimbre = Timbre.Bass;
            public DrumHit[] drums;
            public int[] scale;             // notes de la mélodie
            public Timbre leadTimbre;
            public float leadAmp, leadDensity, leadDur = 0.9f;
            public int seedA, seedB;
        }

        static Style Get(MusicStyle s)
        {
            switch (s)
            {
                case MusicStyle.Tropical:
                    return new Style
                    {
                        bpm = 108, swing = 0.5f, cutoff = 6000,
                        // Do - Sol - La m - Fa : la progression "plage" par excellence.
                        chords = new[] { new[] { 60, 64, 67, 72 }, new[] { 59, 62, 67, 71 }, new[] { 57, 60, 64, 69 }, new[] { 57, 60, 65, 69 } },
                        bass = new[] { 36, 43, 45, 41 },
                        // Grattage de ukulélé "calypso".
                        chordHits = new[] { new Hit(0, 0.5f, 0.09f), new Hit(1, 0.5f, 0.07f), new Hit(1.5f, 0.5f, 0.08f), new Hit(2.5f, 0.5f, 0.08f), new Hit(3, 0.5f, 0.07f), new Hit(3.5f, 0.5f, 0.06f) },
                        chordTimbre = Timbre.Pluck,
                        bassHits = new[] { new Hit(0, 0.9f, 0.34f), new Hit(1.5f, 0.4f, 0.24f), new Hit(2, 0.9f, 0.3f), new Hit(3.5f, 0.4f, 0.22f, 2f) },
                        drums = Concat(
                            new[] { new DrumHit(0, Drum.Kick, 0.45f), new DrumHit(2, Drum.Kick, 0.42f), new DrumHit(1, Drum.Rim, 0.14f), new DrumHit(3, Drum.Rim, 0.14f), new DrumHit(2.75f, Drum.Rim, 0.08f, 1) },
                            Grid(0.25f, Drum.Shaker, 0.03f, 0.05f)),
                        scale = new[] { 72, 74, 76, 79, 81, 84, 86 },
                        leadTimbre = Timbre.Steel, leadAmp = 0.12f, leadDensity = 0.5f, leadDur = 0.6f,
                        seedA = 5, seedB = 17,
                    };
                case MusicStyle.Bossa:
                    return new Style
                    {
                        bpm = 126, swing = 0.5f, cutoff = 4500,
                        // Ré m7 - Sol 7 - Do maj7 - La 7 (ii-V-I-VI).
                        chords = new[] { new[] { 62, 65, 69, 72 }, new[] { 59, 62, 65, 67 }, new[] { 60, 64, 67, 71 }, new[] { 61, 64, 67, 69 } },
                        bass = new[] { 38, 43, 36, 45 },
                        // Accords syncopés typiques de la bossa.
                        chordHits = new[] { new Hit(0, 0.4f, 0.07f), new Hit(1.5f, 0.4f, 0.07f), new Hit(2.5f, 0.4f, 0.06f), new Hit(3.5f, 0.4f, 0.07f) },
                        chordTimbre = Timbre.EPiano,
                        // Basse fondamentale / quinte (x1.5).
                        bassHits = new[] { new Hit(0, 1.4f, 0.32f), new Hit(1.5f, 0.45f, 0.2f, 1.5f), new Hit(2, 1.4f, 0.3f, 1.5f), new Hit(3.5f, 0.45f, 0.2f) },
                        drums = Concat(
                            // Clave bossa (3-2) + grosse caisse douce + charleston.
                            new[] { new DrumHit(0, Drum.Rim, 0.12f), new DrumHit(1.5f, Drum.Rim, 0.12f), new DrumHit(3, Drum.Rim, 0.12f),
                                    new DrumHit(0, Drum.Kick, 0.32f), new DrumHit(1.5f, Drum.Kick, 0.2f), new DrumHit(2, Drum.Kick, 0.3f), new DrumHit(3.5f, Drum.Kick, 0.2f) },
                            Grid(0.5f, Drum.Hat, 0.035f, 0.05f)),
                        scale = new[] { 74, 76, 77, 79, 81, 84 },
                        leadTimbre = Timbre.Flute, leadAmp = 0.1f, leadDensity = 0.35f, leadDur = 1.2f,
                        seedA = 8, seedB = 23,
                    };
                case MusicStyle.Retro8Bit:
                    return new Style
                    {
                        bpm = 128, swing = 0.5f, cutoff = 7000,
                        // La m - Fa - Do - Sol : épique et joyeux.
                        chords = new[] { new[] { 57, 60, 64, 69 }, new[] { 53, 57, 60, 65 }, new[] { 55, 60, 64, 67 }, new[] { 55, 59, 62, 67 } },
                        bass = new[] { 45, 41, 36, 43 },
                        chordHits = new Hit[0], chordTimbre = Timbre.Square, arpAmp = 0.05f,
                        bassTimbre = Timbre.Triangle,
                        bassHits = new[] { new Hit(0, 0.45f, 0.3f), new Hit(0.5f, 0.45f, 0.3f, 2f), new Hit(1, 0.45f, 0.3f), new Hit(1.5f, 0.45f, 0.3f, 2f),
                                           new Hit(2, 0.45f, 0.3f), new Hit(2.5f, 0.45f, 0.3f, 2f), new Hit(3, 0.45f, 0.3f), new Hit(3.5f, 0.45f, 0.3f, 2f) },
                        drums = Concat(
                            new[] { new DrumHit(0, Drum.NoiseKick, 0.4f), new DrumHit(2, Drum.NoiseKick, 0.4f), new DrumHit(2.5f, Drum.NoiseKick, 0.25f, 1),
                                    new DrumHit(1, Drum.NoiseSnare, 0.16f), new DrumHit(3, Drum.NoiseSnare, 0.16f) },
                            Grid(0.5f, Drum.Hat, 0.03f, 0.045f)),
                        scale = new[] { 69, 72, 74, 76, 79, 81, 84 },
                        leadTimbre = Timbre.Square, leadAmp = 0.06f, leadDensity = 0.55f, leadDur = 0.45f,
                        seedA = 4, seedB = 31,
                    };
                default: // Chill
                    return new Style
                    {
                        bpm = 88, swing = 0.62f, cutoff = 4200,
                        chords = new[] { new[] { 60, 64, 67, 71 }, new[] { 57, 60, 64, 67 }, new[] { 53, 57, 60, 64 }, new[] { 55, 59, 62, 65 } },
                        bass = new[] { 36, 33, 41, 43 },
                        chordHits = new[] { new Hit(0, 1.6f, 0.11f), new Hit(1.5f, 1.8f, 0.08f) },
                        chordTimbre = Timbre.EPiano,
                        bassHits = new[] { new Hit(0, 1.6f, 0.32f), new Hit(2, 0.9f, 0.26f), new Hit(3.5f, 0.45f, 0.16f, 2f) },
                        drums = Concat(
                            new[] { new DrumHit(0, Drum.Kick, 0.5f), new DrumHit(2, Drum.Kick, 0.45f), new DrumHit(2.5f, Drum.Kick, 0.3f, 1),
                                    new DrumHit(1, Drum.Snap, 0.16f), new DrumHit(3, Drum.Snap, 0.16f) },
                            Grid(0.5f, Drum.Hat, 0.035f, 0.06f)),
                        scale = new[] { 72, 74, 76, 79, 81, 84 },
                        leadTimbre = Timbre.Marimba, leadAmp = 0.1f, leadDensity = 0.35f,
                        seedA = 3, seedB = 11,
                    };
            }
        }

        static DrumHit[] Grid(float step, Drum drum, float onAmp, float offAmp)
        {
            var list = new List<DrumHit>();
            for (float b = 0; b < BeatsPerBar - 0.001f; b += step)
                list.Add(new DrumHit(b, drum, Math.Abs(b - Math.Round(b)) < 0.01 ? onAmp : offAmp));
            return list.ToArray();
        }

        static DrumHit[] Concat(DrumHit[] a, DrumHit[] b)
        {
            var r = new DrumHit[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        public static float BeatSeconds(MusicStyle s) => 60f / Get(s).bpm;
        public static float LoopSeconds(MusicStyle s) => Bars * BeatsPerBar * BeatSeconds(s);

        /// <summary>Nombre d'échantillons de la boucle à cette fréquence.</summary>
        public static int SampleCount(int rate, MusicStyle s = DefaultStyle) => (int)Math.Round(LoopSeconds(s) * rate);

        /// <summary>Calcule la boucle complète (mono, valeurs entre -Peak et +Peak).</summary>
        public static float[] Render(int rate, MusicStyle styleId = DefaultStyle)
        {
            var st = Get(styleId);
            var buf = new float[SampleCount(rate, styleId)];
            var rng = new Random(7);
            float beat = 60f / st.bpm;

            // Décale les croches "et" pour le swing (0.5 = droit).
            float Swung(float b)
            {
                float whole = (float)Math.Floor(b), frac = b - whole;
                if (Math.Abs(frac - 0.5f) < 0.001f) return whole + st.swing;
                return b;
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                int ci = bar % st.chords.Length;
                float t0 = bar * BeatsPerBar * beat;
                var chord = st.chords[ci];

                foreach (var h in st.chordHits)
                    for (int k = 0; k < chord.Length; k++)
                    {
                        // Ukulélé : notes légèrement décalées, comme un vrai grattage.
                        float strum = st.chordTimbre == Timbre.Pluck ? k * 0.012f : 0f;
                        Note(buf, rate, t0 + Swung(h.beat) * beat + strum, h.dur * beat, Midi(chord[k]), h.amp, st.chordTimbre, rng);
                    }

                if (st.arpAmp > 0f)
                {
                    // Arpège montant puis descendant en doubles croches.
                    int[] order = { 0, 1, 2, 3, 2, 1 };
                    for (int i = 0; i < BeatsPerBar * 4; i++)
                    {
                        int n = chord[order[i % order.Length]] + 12;
                        Note(buf, rate, t0 + i * 0.25f * beat, 0.2f * beat, Midi(n), st.arpAmp, st.chordTimbre, rng);
                    }
                }

                float root = Midi(st.bass[ci]);
                foreach (var h in st.bassHits)
                    Note(buf, rate, t0 + Swung(h.beat) * beat, h.dur * beat, root * h.mul, h.amp, st.bassTimbre, rng);

                foreach (var d in st.drums)
                {
                    if (d.everyOtherBar == 1 && bar % 2 == 0) continue;
                    DrumHitAt(buf, rate, t0 + Swung(d.beat) * beat, d.drum, d.amp, rng);
                }
            }

            // Mélodie : un motif de 4 mesures, rejoué avec une fin différente.
            var motif = MakeMotif(new Random(st.seedA), st);
            var ending = MakeMotif(new Random(st.seedB), st);
            for (int half = 0; half < 2; half++)
                for (int slot = 0; slot < motif.Length; slot++)
                {
                    bool lastBar = slot >= 3 * BeatsPerBar * 2;
                    int note = half == 1 && lastBar ? ending[slot] : motif[slot];
                    if (note < 0) continue;
                    float b = half * 4 * BeatsPerBar + Swung(slot * 0.5f);
                    Note(buf, rate, b * beat, st.leadDur * beat, Midi(note), st.leadAmp, st.leadTimbre, rng);
                }

            LowPassAndNormalize(buf, rate, st.cutoff);
            return buf;
        }

        /// <summary>Motif mélodique sur une grille de croches, par petits pas dans la gamme.</summary>
        static int[] MakeMotif(Random rng, Style st)
        {
            var notes = new int[4 * BeatsPerBar * 2];
            int idx = st.scale.Length / 2;
            for (int i = 0; i < notes.Length; i++)
            {
                bool strong = i % 4 == 0;
                double p = strong ? Math.Min(0.9, st.leadDensity + 0.25) : st.leadDensity;
                if (rng.NextDouble() > p) { notes[i] = -1; continue; }
                idx = Math.Max(0, Math.Min(st.scale.Length - 1, idx + rng.Next(-2, 3)));
                notes[i] = st.scale[idx];
            }
            return notes;
        }

        static float Midi(int n) => 440f * (float)Math.Pow(2.0, (n - 69) / 12.0);

        /// <summary>Ajoute une note ; ce qui dépasse la fin de la boucle revient au début.</summary>
        static void Note(float[] buf, int rate, float start, float length, float freq, float amp, Timbre timbre, Random rng)
        {
            int s0 = (int)(start * rate);
            if (timbre == Timbre.Pluck) { Pluck(buf, rate, s0, length, freq, amp, rng); return; }

            float tail = timbre == Timbre.Bass || timbre == Timbre.Triangle || timbre == Timbre.Square ? 0.04f : timbre == Timbre.Pad ? 0.8f : 0.35f;
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
                        v = Math.Sin(w * t) + 0.3 * Math.Sin(2 * w * t) * Math.Exp(-6 * t);
                        v *= 1.0 + 0.08 * Math.Sin(2 * Math.PI * 4.5 * t);
                        break;
                    case Timbre.Bass:
                        env = Math.Min(1.0, t / 0.006) * Math.Exp(-1.5 * t);
                        v = Math.Sin(w * t) + 0.2 * Math.Sin(2 * w * t);
                        break;
                    case Timbre.Marimba:
                        env = Math.Min(1.0, t / 0.003) * Math.Exp(-5.5 * t);
                        v = Math.Sin(w * t) + 0.25 * Math.Sin(4 * w * t) * Math.Exp(-14 * t);
                        break;
                    case Timbre.Steel:
                        // Steel drum : partiels 1, 2 et 3 qui s'éteignent à des vitesses différentes.
                        env = Math.Min(1.0, t / 0.004) * Math.Exp(-3.5 * t);
                        v = Math.Sin(w * t) + 0.5 * Math.Sin(2 * w * t) * Math.Exp(-4 * t) + 0.25 * Math.Sin(3.01 * w * t) * Math.Exp(-8 * t);
                        break;
                    case Timbre.Square:
                        // Carré à 25 % (son de console 8 bits), sans attaque brutale.
                        env = Math.Min(1.0, t / 0.002) * Math.Exp(-1.2 * t);
                        v = ((t * freq) % 1.0) < 0.25 ? 0.6 : -0.6;
                        break;
                    case Timbre.Triangle:
                        env = Math.Min(1.0, t / 0.002);
                        double ph = (t * freq) % 1.0;
                        v = 4 * Math.Abs(ph - 0.5) - 1;
                        break;
                    case Timbre.Flute:
                        // Sinus doux avec vibrato qui arrive progressivement + un peu de souffle.
                        env = Math.Min(1.0, t / 0.06) * Math.Exp(-0.8 * t);
                        double vib = 1 + 0.006 * Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1.0, t / 0.3);
                        v = Math.Sin(w * vib * t) + 0.12 * Math.Sin(2 * w * vib * t) + 0.04 * (rng.NextDouble() * 2 - 1);
                        break;
                    default: // Pad
                        env = Math.Min(1.0, t / 0.4);
                        v = Math.Sin(w * t) + Math.Sin(w * 1.004 * t);
                        break;
                }
                if (t > length) env *= Math.Max(0.0, 1.0 - (t - length) / tail);
                buf[(s0 + i) % buf.Length] += (float)(v * env * amp);
            }
        }

        /// <summary>Corde pincée (algorithme de Karplus-Strong) : son de ukulélé.</summary>
        static void Pluck(float[] buf, int rate, int s0, float length, float freq, float amp, Random rng)
        {
            int period = Math.Max(2, (int)Math.Round(rate / freq));
            var ring = new double[period];
            for (int i = 0; i < period; i++) ring[i] = rng.NextDouble() * 2 - 1;
            int count = (int)((length + 0.25f) * rate);
            int pos = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                int next = (pos + 1) % period;
                double v = ring[pos];
                ring[pos] = 0.5 * (ring[pos] + ring[next]) * 0.996;
                pos = next;
                double env = t > length ? Math.Max(0.0, 1.0 - (t - length) / 0.25) : 1.0;
                buf[(s0 + i) % buf.Length] += (float)(v * env * amp);
            }
        }

        static void DrumHitAt(float[] buf, int rate, float start, Drum drum, float amp, Random rng)
        {
            int s0 = (int)(start * rate);
            float len = drum == Drum.Kick || drum == Drum.NoiseKick ? 0.35f : drum == Drum.Snap || drum == Drum.NoiseSnare ? 0.18f : drum == Drum.Rim ? 0.06f : 0.05f;
            int count = (int)(len * rate);
            double phase = 0, prevNoise = 0, held = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                double v;
                switch (drum)
                {
                    case Drum.Kick:
                        double f = 45 + 75 * Math.Exp(-30 * t);
                        phase += 2 * Math.PI * f / rate;
                        v = Math.Sin(phase) * Math.Exp(-9 * t);
                        break;
                    case Drum.NoiseKick:
                        // Grosse caisse "console" : triangle dont la hauteur chute vite.
                        double fk = 50 + 150 * Math.Exp(-40 * t);
                        phase = (phase + fk / rate) % 1.0;
                        v = (4 * Math.Abs(phase - 0.5) - 1) * Math.Exp(-10 * t);
                        break;
                    case Drum.Snap:
                        v = ((rng.NextDouble() * 2 - 1) * 0.8 + Math.Sin(2 * Math.PI * 210 * t) * 0.5) * Math.Exp(-26 * t);
                        break;
                    case Drum.NoiseSnare:
                        // Bruit échantillonné grossièrement (son 8 bits).
                        if (i % 4 == 0) held = rng.NextDouble() * 2 - 1;
                        v = held * Math.Exp(-18 * t);
                        break;
                    case Drum.Rim:
                        // Clave / bord de caisse : petit "toc" boisé.
                        v = (Math.Sin(2 * Math.PI * 1700 * t) * 0.7 + Math.Sin(2 * Math.PI * 820 * t) * 0.5) * Math.Exp(-60 * t);
                        break;
                    case Drum.Shaker:
                        double ns = rng.NextDouble() * 2 - 1;
                        v = (ns - prevNoise) * 0.5 * Math.Min(1.0, t / 0.008) * Math.Exp(-45 * t);
                        prevNoise = ns;
                        break;
                    default: // Charleston
                        double n = rng.NextDouble() * 2 - 1;
                        v = (n - prevNoise) * 0.5 * Math.Exp(-70 * t);
                        prevNoise = n;
                        break;
                }
                buf[(s0 + i) % buf.Length] += (float)(v * amp);
            }
        }

        /// <summary>Adoucit les aigus puis ramène le niveau maximal à Peak.</summary>
        static void LowPassAndNormalize(float[] buf, int rate, double cutoff)
        {
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
