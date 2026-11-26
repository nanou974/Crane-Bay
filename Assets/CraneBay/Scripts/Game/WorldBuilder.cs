// -----------------------------------------------------------------------------
// Crane Bay - Décor du terminal (mer, quai, parc, route, voie ferrée).
// Formes simples aux couleurs vives, contours nets : lisibilité avant tout.
// Le décor est construit une seule fois, puis :
//  - recoloré selon le port à thème (tempéré, tropical, polaire) ;
//  - mis à jour quand le parc s'agrandit (emplacements verrouillés grisés) ;
//  - les vagues sont animées ; les rails de la 2e grue apparaissent à l'achat.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class WorldBuilder : MonoBehaviour
    {
        Transform[] waves;
        Vector3[] waveBase;
        SpriteRenderer water, quay, yard, grass;
        readonly List<SpriteRenderer> waveSrs = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> bushes = new List<SpriteRenderer>();
        readonly List<GameObject> lockedOverlays = new List<GameObject>();
        GameObject secondCraneRails;

        /// <summary>Positions des lampadaires (utilisées par l'éclairage de nuit).</summary>
        public readonly List<Vector3> lampPositions = new List<Vector3>();

        public void Build()
        {
            var t = transform;
            const float W = 16f; // plus large que l'écran pour couvrir tous les formats

            // --- Mer ---
            water = SpriteFactory.Make("Mer", t, SpriteFactory.Plain, Palette.Water, new Vector2(W, 12f), new Vector3(0, BoardLayout.WaterBottom + 6f, 0), 0);
            var rng = new System.Random(4);
            waves = new Transform[14];
            waveBase = new Vector3[waves.Length];
            for (int i = 0; i < waves.Length; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 11 - 5.5), BoardLayout.WaterBottom + 0.5f + (float)(rng.NextDouble() * 5.5), 0);
                var w = SpriteFactory.Make("Vague", t, SpriteFactory.SoftDot, new Color(1, 1, 1, 0.35f), new Vector2(0.9f, 0.12f), p, 1);
                waves[i] = w.transform;
                waveBase[i] = p;
                waveSrs.Add(w);
            }

            // --- Quai ---
            float quayH = BoardLayout.QuayTop - BoardLayout.QuayBottom;
            quay = SpriteFactory.Make("Quai", t, SpriteFactory.Plain, Palette.Quay, new Vector2(W, quayH), new Vector3(0, (BoardLayout.QuayTop + BoardLayout.QuayBottom) * 0.5f, 0), 2);
            SpriteFactory.Make("BordQuai", t, SpriteFactory.Plain, Palette.QuayEdge, new Vector2(W, 0.09f), new Vector3(0, BoardLayout.QuayTop, 0), 3);
            for (float x = -4.2f; x <= 4.3f; x += 1.4f)
                SpriteFactory.Make("Bitte", t, SpriteFactory.Circle, Palette.Deck, new Vector2(0.2f, 0.2f), new Vector3(x, BoardLayout.QuayTop - 0.18f, 0), 4);
            SpriteFactory.Make("BandeSecurite", t, SpriteFactory.Plain, Palette.YardMarking, new Vector2(W, 0.06f), new Vector3(0, BoardLayout.QuayBottom + 0.12f, 0), 3);
            foreach (var lx in new[] { -2.8f, 0f, 2.8f }) AddLamp(t, new Vector3(lx, BoardLayout.QuayBottom + 0.35f, 0));

            // --- Parc de stockage ---
            float yardTop = BoardLayout.QuayBottom, yardBottom = BoardLayout.YardBottom;
            yard = SpriteFactory.Make("Parc", t, SpriteFactory.Plain, Palette.Asphalt, new Vector2(W, yardTop - yardBottom), new Vector3(0, (yardTop + yardBottom) * 0.5f, 0), 1);
            for (int s = 0; s < GameRules.YardColumns * GameRules.YardRows; s++)
            {
                var center = BoardLayout.YardSlotCenter(s);
                SpriteFactory.Make("Emplacement" + s, t, SpriteFactory.Ring, new Color(Palette.YardMarking.r, Palette.YardMarking.g, Palette.YardMarking.b, 0.75f),
                    BoardLayout.ContainerSize + new Vector2(0.14f, 0.14f), center, 3);
                if (PortModel.IsPoweredSlot(s))
                {
                    // Prise électrique pour frigorifiques : borne jaune à gauche de l'emplacement.
                    SpriteFactory.Make("Prise", t, SpriteFactory.RoundedSmall, Palette.Power, new Vector2(0.14f, 0.26f),
                        center + new Vector3(-BoardLayout.ContainerSize.x * 0.5f - 0.12f, 0.12f, 0), 4);
                }
                // Voile grisé + barres croisées pour un emplacement verrouillé.
                var lockGo = new GameObject("Verrou" + s);
                lockGo.transform.SetParent(t, false);
                lockGo.transform.localPosition = center;
                SpriteFactory.Make("Voile", lockGo.transform, SpriteFactory.Rounded, new Color(0.25f, 0.28f, 0.35f, 0.8f),
                    BoardLayout.ContainerSize + new Vector2(0.14f, 0.14f), Vector3.zero, 4);
                foreach (var ang in new[] { 25f, -25f })
                {
                    var bar = SpriteFactory.Make("Croix", lockGo.transform, SpriteFactory.Plain, new Color(1f, 1f, 1f, 0.35f), new Vector2(1.2f, 0.07f), Vector3.zero, 5);
                    bar.transform.localRotation = Quaternion.Euler(0, 0, ang);
                }
                lockedOverlays.Add(lockGo);
            }

            // --- Route des camions ---
            float roadTop = yardBottom, roadBottom = BoardLayout.RoadY - 0.85f;
            SpriteFactory.Make("Route", t, SpriteFactory.Plain, Palette.Road, new Vector2(W, roadTop - roadBottom), new Vector3(0, (roadTop + roadBottom) * 0.5f, 0), 1);
            SpriteFactory.Make("BordRoute", t, SpriteFactory.Plain, Palette.RoadMarking, new Vector2(W, 0.05f), new Vector3(0, roadTop - 0.06f, 0), 2);
            SpriteFactory.Make("BordRoute2", t, SpriteFactory.Plain, Palette.RoadMarking, new Vector2(W, 0.05f), new Vector3(0, roadBottom + 0.06f, 0), 2);
            foreach (var bx in BoardLayout.TruckBayX)
            {
                SpriteFactory.Make("PlaceCamion", t, SpriteFactory.Ring, new Color(1, 1, 1, 0.45f), new Vector2(3.1f, 1.1f),
                    new Vector3(bx - 0.45f, BoardLayout.RoadY, 0), 2);
            }

            // --- Voie ferrée ---
            float railTop = roadBottom, railBottom = BoardLayout.RailY - 0.6f;
            SpriteFactory.Make("Ballast", t, SpriteFactory.Plain, Palette.Ballast, new Vector2(W, railTop - railBottom), new Vector3(0, (railTop + railBottom) * 0.5f, 0), 1);
            for (float x = -7.8f; x <= 7.8f; x += 0.45f)
                SpriteFactory.Make("Traverse", t, SpriteFactory.Plain, Palette.Sleeper, new Vector2(0.16f, 0.86f), new Vector3(x, BoardLayout.RailY, 0), 2);
            SpriteFactory.Make("Rail1", t, SpriteFactory.Plain, Palette.Rail, new Vector2(W, 0.07f), new Vector3(0, BoardLayout.RailY + 0.27f, 0), 3);
            SpriteFactory.Make("Rail2", t, SpriteFactory.Plain, Palette.Rail, new Vector2(W, 0.07f), new Vector3(0, BoardLayout.RailY - 0.27f, 0), 3);
            foreach (var lx in new[] { -3.5f, 1.0f, 4.2f }) AddLamp(t, new Vector3(lx, BoardLayout.RoadY + 0.62f, 0));

            // --- Herbe / sable / neige (sous l'interface du bas) ---
            grass = SpriteFactory.Make("Herbe", t, SpriteFactory.Plain, Palette.Grass, new Vector2(W, 10f), new Vector3(0, railBottom - 5f, 0), 1);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 10 - 5), railBottom - 0.4f - (float)(rng.NextDouble() * 3), 0);
                bushes.Add(SpriteFactory.Make("Buisson", t, SpriteFactory.Circle, Palette.GrassDark, new Vector2(0.45f, 0.45f), p, 2));
            }

            // --- Rails du portique principal (le long des bords) ---
            foreach (var sx in new[] { -1f, 1f })
                SpriteFactory.Make("RailGrue", t, SpriteFactory.Plain, Palette.QuayEdge, new Vector2(0.08f, BoardLayout.QuayTop - railBottom),
                    new Vector3(sx * BoardLayout.CraneLegX, (BoardLayout.QuayTop + railBottom) * 0.5f, 0), 4);

            // --- Rails de la 2e grue (horizontaux, en haut du parc et sous la voie ferrée) ---
            secondCraneRails = new GameObject("RailsGrue2");
            secondCraneRails.transform.SetParent(t, false);
            foreach (var y in new[] { BoardLayout.SecondCraneTopY, BoardLayout.SecondCraneBottomY })
                SpriteFactory.Make("Rail", secondCraneRails.transform, SpriteFactory.Plain, Palette.QuayEdge, new Vector2(W, 0.08f), new Vector3(0, y, 0), 4);
            secondCraneRails.SetActive(false);
        }

        void AddLamp(Transform t, Vector3 p)
        {
            SpriteFactory.Make("Lampadaire", t, SpriteFactory.Circle, Palette.Deck, new Vector2(0.16f, 0.16f), p, 4);
            lampPositions.Add(p);
        }

        /// <summary>Recolore le décor pour le port à thème du niveau.</summary>
        public void ApplyTheme(PortTheme theme)
        {
            var th = Palette.Themes[(int)theme];
            water.color = th.water;
            quay.color = th.quay;
            yard.color = th.asphalt;
            grass.color = th.grass;
            foreach (var b in bushes) b.color = th.bush;
            foreach (var w in waveSrs) { var c = th.waterLight; c.a = 0.35f; w.color = c; }
        }

        /// <summary>Grise les emplacements pas encore débloqués.</summary>
        public void SetUnlockedRows(int rows)
        {
            for (int s = 0; s < lockedOverlays.Count; s++)
                lockedOverlays[s].SetActive(s / GameRules.YardColumns >= rows);
        }

        public void SetSecondCrane(bool owned) => secondCraneRails.SetActive(owned);

        void Update()
        {
            if (waves == null) return;
            // Vagues : léger va-et-vient horizontal, déphasé pour chaque vague.
            float time = Time.time;
            for (int i = 0; i < waves.Length; i++)
            {
                var b = waveBase[i];
                waves[i].localPosition = new Vector3(b.x + Mathf.Sin(time * 0.6f + i * 1.7f) * 0.35f, b.y, 0);
            }
        }
    }
}
