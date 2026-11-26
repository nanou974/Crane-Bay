// -----------------------------------------------------------------------------
// Crane Bay - Camions et trains (vue de dessus).
// Chaque place à remplir affiche une "silhouette" translucide de la couleur
// demandée avec sa valeur : le joueur voit immédiatement quoi livrer.
// Une jauge de patience indique combien de temps le véhicule attendra encore.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class VehicleView : MonoBehaviour
    {
        public Vehicle Model { get; private set; }
        public bool Docked { get; private set; }
        public bool Gone { get; private set; }

        readonly List<Transform> cargoAnchors = new List<Transform>();
        readonly List<GameObject> ghosts = new List<GameObject>();
        readonly List<ContainerView> loaded = new List<ContainerView>();
        Transform patienceFill;
        SpriteRenderer patienceFillSr;
        GameObject patienceRoot;
        TextMesh fastLabel;

        float dockX, startX, exitX;
        float timer;
        bool leaving;
        float leaveDelay;
        const float ArriveTime = 2.2f;
        const float LeaveTime = 2.0f;
        const float PatienceWidth = 1.2f;

        public System.Action<VehicleView> OnDocked;
        public System.Action<VehicleView> OnGone;

        public IReadOnlyList<ContainerView> Loaded => loaded;

        public static VehicleView Create(Transform parent, Vehicle v, System.Random rng)
        {
            var go = new GameObject(v.kind == VehicleKind.Train ? "Train" : "Camion");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<VehicleView>();
            view.Model = v;
            if (v.kind == VehicleKind.Train) view.BuildTrain();
            else view.BuildTruck(Palette.TruckCabs[rng.Next(Palette.TruckCabs.Length)]);
            return view;
        }

        void BuildTruck(Color cab)
        {
            var t = transform;
            dockX = BoardLayout.TruckBayX[Model.bay];
            startX = 8.5f;
            exitX = -9f;
            transform.position = new Vector3(startX, BoardLayout.RoadY, 0f);

            SpriteFactory.Make("Ombre", t, SpriteFactory.Shadow, Palette.Shadow, new Vector2(3.2f, 1.25f), new Vector3(-0.45f, -0.08f, 0f), 4);
            // Roues qui dépassent du châssis (crédibilité vue de dessus).
            foreach (var wx in new[] { -1.35f, 0.25f, 0.65f })
                foreach (var wy in new[] { 0.42f, -0.42f })
                    SpriteFactory.Make("Roue", t, SpriteFactory.RoundedSmall, Palette.Outline, new Vector2(0.32f, 0.14f), new Vector3(wx, wy, 0f), 5);
            SpriteFactory.Make("Remorque", t, SpriteFactory.Rounded, Palette.Chassis, new Vector2(1.85f, 0.86f), Vector3.zero, 6);
            SpriteFactory.Make("Cabine", t, SpriteFactory.Rounded, cab, new Vector2(0.72f, 0.82f), new Vector3(BoardLayout.TruckCabOffset, 0f, 0f), 7);
            SpriteFactory.Make("PareBrise", t, SpriteFactory.RoundedSmall, Palette.Window, new Vector2(0.16f, 0.6f), new Vector3(BoardLayout.TruckCabOffset - 0.2f, 0f, 0f), 8);

            AddCargoSlot(t, Vector3.zero, Model.demands[0]);
            BuildPatience(t, new Vector3(-0.2f, 0.62f, 0f));
        }

        void BuildTrain()
        {
            var t = transform;
            dockX = 0f; // le repère du train est aligné sur la voie
            startX = 11f;
            exitX = -15f;
            transform.position = new Vector3(startX, BoardLayout.RailY, 0f);
            float baseY = 0f;

            // Locomotive.
            var locoPos = new Vector3(BoardLayout.LocoX, baseY, 0f);
            SpriteFactory.Make("OmbreLoco", t, SpriteFactory.Shadow, Palette.Shadow, new Vector2(2.0f, 1.2f), locoPos + new Vector3(0.06f, -0.08f, 0f), 4);
            SpriteFactory.Make("Loco", t, SpriteFactory.Rounded, Palette.Loco, new Vector2(1.6f, 0.82f), locoPos, 6);
            SpriteFactory.Make("Bande", t, SpriteFactory.Plain, Palette.LocoStripe, new Vector2(1.45f, 0.1f), locoPos, 7);
            SpriteFactory.Make("Cabine", t, SpriteFactory.RoundedSmall, Palette.Window, new Vector2(0.22f, 0.62f), locoPos + new Vector3(-0.55f, 0f, 0f), 8);
            SpriteFactory.Make("Ventilo", t, SpriteFactory.Circle, Palette.Wagon, new Vector2(0.32f, 0.32f), locoPos + new Vector3(0.35f, 0f, 0f), 8);

            for (int i = 0; i < Model.demands.Count; i++)
            {
                var p = BoardLayout.WagonCenter(i);
                p.y = baseY;
                SpriteFactory.Make("Attelage", t, SpriteFactory.Plain, Palette.Outline, new Vector2(0.3f, 0.08f), p + new Vector3(-0.92f, 0f, 0f), 5);
                SpriteFactory.Make("OmbreWagon", t, SpriteFactory.Shadow, Palette.Shadow, new Vector2(2.0f, 1.1f), p + new Vector3(0.06f, -0.08f, 0f), 4);
                SpriteFactory.Make("Wagon", t, SpriteFactory.Rounded, Palette.Wagon, new Vector2(1.72f, 0.72f), p, 6);
                AddCargoSlot(t, p, Model.demands[i]);
            }
            BuildPatience(t, locoPos + new Vector3(0f, 0.62f, 0f));
        }

        void AddCargoSlot(Transform t, Vector3 localPos, Demand d)
        {
            var anchor = new GameObject("Chargement").transform;
            anchor.SetParent(t, false);
            anchor.localPosition = localPos;
            cargoAnchors.Add(anchor);

            // Silhouette de la demande : couleur translucide + contour blanc + valeur.
            var ghost = new GameObject("Demande");
            ghost.transform.SetParent(anchor, false);
            var col = Palette.Of(d.color);
            col.a = 0.55f;
            SpriteFactory.Make("Silhouette", ghost.transform, SpriteFactory.Container, col, BoardLayout.ContainerSize * 0.92f, Vector3.zero, 9);
            SpriteFactory.Make("Cadre", ghost.transform, SpriteFactory.Ring, Color.white, BoardLayout.ContainerSize + new Vector2(0.12f, 0.12f), Vector3.zero, 9);
            SpriteFactory.MakeText("Valeur", ghost.transform, GameRules.Payout(d.color, Model.kind == VehicleKind.Train) + " $", 0.3f,
                Palette.LabelOf(d.color), Vector3.zero, 9);
            ghosts.Add(ghost);
        }

        void BuildPatience(Transform t, Vector3 pos)
        {
            patienceRoot = new GameObject("Patience");
            patienceRoot.transform.SetParent(t, false);
            patienceRoot.transform.localPosition = pos;
            SpriteFactory.Make("Fond", patienceRoot.transform, SpriteFactory.RoundedSmall, Palette.Outline, new Vector2(PatienceWidth + 0.08f, 0.16f), Vector3.zero, 8);
            patienceFillSr = SpriteFactory.Make("Jauge", patienceRoot.transform, SpriteFactory.Plain, Palette.UiGreen, new Vector2(PatienceWidth, 0.08f), Vector3.zero, 9);
            patienceFill = patienceFillSr.transform;
            // Bonus de rapidité : affiché tant que le véhicule vient d'arriver.
            fastLabel = SpriteFactory.MakeText("Rapide", patienceRoot.transform, "RAPIDE +20 %", 0.22f, Palette.UiGold, new Vector3(0f, 0.2f, 0f), 9);
            patienceRoot.SetActive(false);
        }

        /// <summary>Position monde où la grue doit déposer pour la place "index".</summary>
        public Vector3 CargoWorld(int index)
        {
            index = Mathf.Clamp(index, 0, cargoAnchors.Count - 1);
            return cargoAnchors[index].position;
        }

        public Vector2 CargoHitSize => BoardLayout.SlotHitSize;

        public int CargoCount => cargoAnchors.Count;

        /// <summary>Le conteneur déposé par la grue est attaché au véhicule.</summary>
        public void AttachContainer(int index, ContainerView cv)
        {
            index = Mathf.Clamp(index, 0, cargoAnchors.Count - 1);
            cv.transform.SetParent(cargoAnchors[index], false);
            cv.transform.localPosition = Vector3.zero;
            cv.SetTier(0);
            cv.PlayLanding();
            ghosts[index].SetActive(false);
            loaded.Add(cv);
        }

        public void Leave(float delay)
        {
            if (leaving) return;
            leaving = true;
            leaveDelay = delay;
            Docked = false;
            timer = 0f;
            startX = transform.position.x;
            patienceRoot.SetActive(false);
            foreach (var g in ghosts) g.SetActive(false);
        }

        void Update()
        {
            if (Gone) return;
            if (leaving)
            {
                if (leaveDelay > 0f) { leaveDelay -= Time.deltaTime; return; }
                timer += Time.deltaTime;
                float k = Mathf.Clamp01(timer / LeaveTime);
                SetX(Mathf.Lerp(startX, exitX, k * k));
                if (k >= 1f) { Gone = true; OnGone?.Invoke(this); }
                return;
            }
            if (!Docked)
            {
                timer += Time.deltaTime;
                float k = Mathf.Clamp01(timer / ArriveTime);
                float e = 1f - (1f - k) * (1f - k);
                SetX(Mathf.Lerp(startX, dockX, e));
                if (k >= 1f)
                {
                    Docked = true;
                    patienceRoot.SetActive(true);
                    OnDocked?.Invoke(this);
                }
            }
        }

        void SetX(float x)
        {
            var p = transform.position;
            p.x = x;
            transform.position = p;
        }

        /// <summary>Affiche ou masque l'étiquette du bonus de rapidité (clignote légèrement).</summary>
        public void SetFast(bool active)
        {
            if (fastLabel == null) return;
            if (fastLabel.gameObject.activeSelf != active) fastLabel.gameObject.SetActive(active);
            if (active)
            {
                var c = Palette.UiGold;
                c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 10f);
                fastLabel.color = c;
            }
        }

        /// <summary>Met à jour la jauge de patience (vert -> orange -> rouge).</summary>
        public void SetPatience(float ratio)
        {
            if (patienceFill == null) return;
            ratio = Mathf.Clamp01(ratio);
            patienceFill.localScale = new Vector3(PatienceWidth * ratio, 0.08f, 1f);
            patienceFill.localPosition = new Vector3(-PatienceWidth * (1f - ratio) * 0.5f, 0f, 0f);
            patienceFillSr.color = ratio > 0.5f ? Palette.UiGreen : Color.Lerp(new Color(0.95f, 0.25f, 0.2f), Palette.UiGold, ratio * 2f);
        }
    }
}
