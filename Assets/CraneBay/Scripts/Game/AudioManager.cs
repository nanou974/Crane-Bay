// -----------------------------------------------------------------------------
// Crane Bay - Sons procéduraux.
// Les bruitages sont synthétisés au lancement (pas de fichiers audio) : simples
// mais "satisfaisants" (clac métallique, pièces, corne de brume...).
// Un seul AudioSource + PlayOneShot : très léger pour le processeur.
// -----------------------------------------------------------------------------
using System;
using UnityEngine;

namespace CraneBay.Game
{
    public enum Sfx { Select, Grab, Drop, Coin, Chest, Error, Horn, Honk, Upgrade }

    public class AudioManager : MonoBehaviour
    {
        const int Rate = 22050;
        const string MuteKey = "cranebay_mute";

        AudioSource source;
        AudioClip[] clips;
        float lastCoinTime;

        public bool Muted { get; private set; }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

            clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            clips[(int)Sfx.Select] = Build("select", 0.04f, t => Tone(t, 1400) * Env(t, 0.04f, 0.002f));
            clips[(int)Sfx.Grab] = Build("grab", 0.16f, t => (Tone(t, 110) * 0.8f + Noise() * 0.25f) * Env(t, 0.16f, 0.004f));
            clips[(int)Sfx.Drop] = Build("drop", 0.26f, t =>
                (Square(t, 190) * 0.35f + Tone(t, 430) * 0.35f + Tone(t, 760) * 0.15f + Noise() * 0.3f * Env(t, 0.05f, 0.001f)) * Env(t, 0.26f, 0.002f));
            clips[(int)Sfx.Coin] = Build("coin", 0.22f, t => Tone(t, t < 0.07f ? 988 : 1319) * Env(t, 0.22f, 0.003f));
            clips[(int)Sfx.Chest] = Build("chest", 0.5f, t =>
            {
                float[] notes = { 523, 659, 784, 1047 };
                int i = Mathf.Min(3, (int)(t / 0.09f));
                return (Tone(t, notes[i]) * 0.7f + Tone(t, notes[i] * 2) * 0.15f) * Env(t, 0.5f, 0.005f);
            });
            clips[(int)Sfx.Error] = Build("error", 0.2f, t => Square(t, 140) * 0.45f * Env(t, 0.2f, 0.005f));
            clips[(int)Sfx.Horn] = Build("horn", 1.1f, t =>
                (Saw(t, 98) * 0.35f + Saw(t, 147) * 0.25f) * Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1.1f - t) * 4f));
            clips[(int)Sfx.Honk] = Build("honk", 0.28f, t =>
                (Square(t, 392) * 0.25f + Square(t, 494) * 0.2f) * Mathf.Clamp01(t * 40f) * Mathf.Clamp01((0.28f - t) * 20f));
            clips[(int)Sfx.Upgrade] = Build("upgrade", 0.35f, t => Tone(t, 400 + 1400 * t) * Env(t, 0.35f, 0.01f));
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (Muted || clips == null) return;
            // Évite l'accumulation de "ding" lors de plusieurs paiements rapprochés.
            if (sfx == Sfx.Coin)
            {
                if (Time.unscaledTime - lastCoinTime < 0.05f) return;
                lastCoinTime = Time.unscaledTime;
            }
            source.PlayOneShot(clips[(int)sfx], volume);
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        // --- Synthèse -------------------------------------------------------------

        static readonly System.Random noiseRng = new System.Random(12345);

        static float Tone(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
        static float Saw(float t, float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
        static float Noise() => (float)(noiseRng.NextDouble() * 2.0 - 1.0);

        /// <summary>Enveloppe attaque rapide / décroissance exponentielle.</summary>
        static float Env(float t, float length, float attack)
        {
            float a = Mathf.Clamp01(t / attack);
            float d = Mathf.Exp(-5f * t / length);
            return a * d;
        }

        static AudioClip Build(string name, float seconds, Func<float, float> gen)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(gen((float)i / Rate) * 0.6f, -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
