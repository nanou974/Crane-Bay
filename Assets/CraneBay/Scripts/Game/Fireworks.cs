// -----------------------------------------------------------------------------
// Crane Bay - Feu d'artifice du passage de niveau.
// Des fusées montent du bas de l'écran puis éclatent en gerbes d'étincelles
// colorées qui retombent (gravité) en scintillant. Réalisé avec des sprites de
// l'atlas recyclés (aucune allocation pendant l'effet, aucun système de
// particules) : léger pour les téléphones d'entrée de gamme.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace CraneBay.Game
{
    public class Fireworks : MonoBehaviour
    {
        class Rocket { public SpriteRenderer sr; public Vector2 pos, vel; public float fuse; public Color color; public bool active; }
        class Spark { public SpriteRenderer sr; public Vector2 pos, vel; public float life, maxLife; public Color color; public bool active; }

        const int SparksPerBurst = 30;
        const float Gravity = -3.2f;

        readonly List<Rocket> rockets = new List<Rocket>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly System.Random rng = new System.Random();
        float launchTimer;
        int toLaunch;
        AudioManager audioManager;

        static readonly Color[] Colors =
        {
            new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.25f), new Color(0.35f, 0.9f, 1f),
            new Color(0.5f, 1f, 0.45f), new Color(0.95f, 0.5f, 1f), new Color(1f, 0.6f, 0.2f), Color.white,
        };

        public bool Playing => toLaunch > 0 || rockets.Exists(r => r.active) || sparks.Exists(s => s.active);

        public void Init(AudioManager audio) => audioManager = audio;

        /// <summary>Lance un spectacle de "count" fusées, décalées dans le temps.</summary>
        public void Play(int count = 8)
        {
            toLaunch += count;
            launchTimer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (toLaunch > 0)
            {
                launchTimer -= dt;
                if (launchTimer <= 0f)
                {
                    Launch();
                    toLaunch--;
                    launchTimer = 0.25f + (float)rng.NextDouble() * 0.3f;
                }
            }

            foreach (var r in rockets)
            {
                if (!r.active) continue;
                r.vel.y += Gravity * 0.35f * dt;
                r.pos += r.vel * dt;
                r.fuse -= dt;
                r.sr.transform.position = r.pos;
                if (r.fuse <= 0f)
                {
                    r.active = false;
                    r.sr.gameObject.SetActive(false);
                    Burst(r.pos, r.color);
                }
            }

            foreach (var s in sparks)
            {
                if (!s.active) continue;
                s.life -= dt;
                if (s.life <= 0f) { s.active = false; s.sr.gameObject.SetActive(false); continue; }
                s.vel *= 1f - 1.6f * dt;      // freinage de l'air
                s.vel.y += Gravity * dt;
                s.pos += s.vel * dt;
                float k = s.life / s.maxLife;
                // Scintillement en fin de vie.
                float twinkle = k < 0.4f ? (Mathf.Sin(s.life * 60f) * 0.5f + 0.5f) : 1f;
                var c = s.color;
                c.a = Mathf.Clamp01(k * 1.5f) * twinkle;
                s.sr.color = c;
                float size = 0.12f + 0.12f * k;
                s.sr.transform.localScale = new Vector3(size, size, 1f);
                s.sr.transform.position = s.pos;
            }
        }

        void Launch()
        {
            var r = rockets.Find(x => !x.active);
            if (r == null)
            {
                r = new Rocket { sr = SpriteFactory.Make("Fusee", transform, SpriteFactory.SoftDot, Color.white, new Vector2(0.18f, 0.32f), Vector3.zero, 90) };
                rockets.Add(r);
            }
            r.color = Colors[rng.Next(Colors.Length)];
            r.pos = new Vector2((float)(rng.NextDouble() * 7 - 3.5), -6.5f);
            r.vel = new Vector2((float)(rng.NextDouble() * 1.2 - 0.6), 9f + (float)rng.NextDouble() * 2.5f);
            r.fuse = 0.9f + (float)rng.NextDouble() * 0.35f;
            r.active = true;
            r.sr.color = Color.Lerp(r.color, Color.white, 0.5f);
            r.sr.gameObject.SetActive(true);
            r.sr.transform.position = r.pos;
            audioManager?.Play(Sfx.Whistle, 0.35f);
        }

        void Burst(Vector2 at, Color color)
        {
            audioManager?.Play(Sfx.Pop, 0.8f);
            bool twoTone = rng.NextDouble() < 0.4;
            Color second = Colors[rng.Next(Colors.Length)];
            for (int i = 0; i < SparksPerBurst; i++)
            {
                var s = sparks.Find(x => !x.active);
                if (s == null)
                {
                    s = new Spark { sr = SpriteFactory.Make("Etincelle", transform, SpriteFactory.SoftDot, Color.white, new Vector2(0.2f, 0.2f), Vector3.zero, 91) };
                    sparks.Add(s);
                }
                float ang = (i / (float)SparksPerBurst) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                float speed = 3.2f + (float)rng.NextDouble() * 1.4f;
                s.pos = at;
                s.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                s.maxLife = s.life = 1.1f + (float)rng.NextDouble() * 0.6f;
                s.color = twoTone && (i % 2 == 0) ? second : color;
                s.active = true;
                s.sr.gameObject.SetActive(true);
                s.sr.transform.position = at;
            }
        }
    }
}
