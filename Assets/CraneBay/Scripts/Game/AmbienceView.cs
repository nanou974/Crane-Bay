// -----------------------------------------------------------------------------
// Crane Bay - Ambiance : nuit, lampadaires, vent et pluie.
// Tout est fait avec quelques sprites de l'atlas (un voile sombre, des halos,
// des traînées) : très peu coûteux, pas de lumière ni de particules Unity.
// L'état (heure du cycle, météo) vient de Core/Ambience.cs.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class AmbienceView : MonoBehaviour
    {
        const int WindCount = 14;
        const int RainCount = 40;

        SpriteRenderer nightVeil;
        readonly List<SpriteRenderer> lampGlows = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> wind = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> rain = new List<SpriteRenderer>();
        readonly System.Random rng = new System.Random(77);
        float windAlpha, rainAlpha;

        public void Build(IEnumerable<Vector3> lamps)
        {
            var t = transform;
            nightVeil = SpriteFactory.Make("Nuit", t, SpriteFactory.Plain, new Color(Palette.Night.r, Palette.Night.g, Palette.Night.b, 0f),
                new Vector2(20f, 40f), Vector3.zero, 60);
            foreach (var p in lamps)
                lampGlows.Add(SpriteFactory.Make("Halo", t, SpriteFactory.SoftDot, new Color(Palette.Lamp.r, Palette.Lamp.g, Palette.Lamp.b, 0f),
                    new Vector2(2.2f, 2.2f), p, 61));
            for (int i = 0; i < WindCount; i++)
            {
                var w = SpriteFactory.Make("Vent", t, SpriteFactory.SoftDot, new Color(1, 1, 1, 0f), new Vector2(1.6f, 0.06f), RandomPos(), 62);
                wind.Add(w);
            }
            for (int i = 0; i < RainCount; i++)
            {
                var r = SpriteFactory.Make("Pluie", t, SpriteFactory.Plain, new Color(0.8f, 0.9f, 1f, 0f), new Vector2(0.03f, 0.32f), RandomPos(), 62);
                r.transform.localRotation = Quaternion.Euler(0, 0, -15f);
                rain.Add(r);
            }
        }

        Vector3 RandomPos() => new Vector3((float)(rng.NextDouble() * 12 - 6), (float)(rng.NextDouble() * 20 - 10), 0f);

        void Update()
        {
            if (nightVeil == null) return;
            double now = TimeUtil.NowUnix;
            float night = Ambience.NightAmount(now);
            var weather = Ambience.WeatherAt(now);
            float dt = Time.deltaTime;

            var c = nightVeil.color;
            c.a = 0.42f * night;
            nightVeil.color = c;
            foreach (var g in lampGlows)
            {
                var gc = g.color;
                gc.a = 0.55f * night;
                g.color = gc;
            }

            // Fondu doux quand la météo change.
            windAlpha = Mathf.MoveTowards(windAlpha, weather == Weather.Vent ? 1f : 0f, dt);
            rainAlpha = Mathf.MoveTowards(rainAlpha, weather == Weather.Pluie ? 1f : 0f, dt);

            if (windAlpha > 0f)
                foreach (var w in wind)
                {
                    var p = w.transform.localPosition;
                    p.x += 9f * dt;
                    if (p.x > 7f) p = new Vector3(-7f, (float)(rng.NextDouble() * 20 - 10), 0f);
                    w.transform.localPosition = p;
                    w.color = new Color(1, 1, 1, 0.35f * windAlpha);
                }
            else foreach (var w in wind) if (w.color.a > 0f) w.color = new Color(1, 1, 1, 0f);

            if (rainAlpha > 0f)
                foreach (var r in rain)
                {
                    var p = r.transform.localPosition;
                    p += new Vector3(2.5f, -14f, 0f) * dt;
                    if (p.y < -11f) p = new Vector3((float)(rng.NextDouble() * 12 - 7), 11f, 0f);
                    r.transform.localPosition = p;
                    r.color = new Color(0.8f, 0.9f, 1f, 0.45f * rainAlpha);
                }
            else foreach (var r in rain) if (r.color.a > 0f) r.color = new Color(0.8f, 0.9f, 1f, 0f);
        }
    }
}
