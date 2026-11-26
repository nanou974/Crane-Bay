// -----------------------------------------------------------------------------
// Crane Bay - Porte-conteneurs (vue de dessus).
// Arrive par la gauche, se met à quai, repart vers la droite une fois vidé.
// Les conteneurs à bord sont enfants du navire : ils se déplacent avec lui.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CraneBay.Game
{
    public class ShipView : MonoBehaviour
    {
        public enum State { Absent, Arriving, Docked, Leaving }

        public State Current { get; private set; } = State.Absent;
        public bool IsDocked => Current == State.Docked;

        /// <summary>Racine des conteneurs à bord (repère local du pont).</summary>
        public Transform Deck { get; private set; }

        const float TravelTime = 3.2f;
        float timer;
        float startX, endX;
        SpriteRenderer wake;

        public System.Action Arrived;
        public System.Action Left;

        public static ShipView Create(Transform parent)
        {
            var go = new GameObject("Navire");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ShipView>();
            view.Build();
            go.SetActive(false);
            return view;
        }

        void Build()
        {
            var t = transform;
            // Ombre portée sur l'eau.
            SpriteFactory.Make("Ombre", t, SpriteFactory.Shadow, new Color(0.05f, 0.2f, 0.35f, 0.35f),
                new Vector2(10.4f, 3.0f), new Vector3(0.15f, -0.2f, 0f), 3);
            // Proue pointue : un carré arrondi tourné à 45°.
            var bow = SpriteFactory.Make("Proue", t, SpriteFactory.Rounded, Palette.Hull,
                new Vector2(1.65f, 1.65f), new Vector3(-4.55f, 0f, 0f), 4);
            bow.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            bow.transform.localScale = new Vector3(1f, 1f, 1f);
            SpriteFactory.Make("Coque", t, SpriteFactory.Rounded, Palette.Hull, new Vector2(9.4f, 2.35f), new Vector3(0.1f, 0f, 0f), 5);
            SpriteFactory.Make("Pont", t, SpriteFactory.Rounded, Palette.Deck, new Vector2(8.0f, 1.95f), new Vector3(-0.45f, 0f, 0f), 6);
            // Château (passerelle) à l'arrière, avec vitres et cheminée.
            SpriteFactory.Make("Chateau", t, SpriteFactory.Rounded, Palette.Bridge, new Vector2(1.0f, 2.05f), new Vector3(4.15f, 0f, 0f), 7);
            SpriteFactory.Make("Vitres", t, SpriteFactory.RoundedSmall, Palette.Window, new Vector2(0.22f, 1.6f), new Vector3(3.82f, 0f, 0f), 8);
            SpriteFactory.Make("Cheminee", t, SpriteFactory.Circle, Palette.Hull, new Vector2(0.42f, 0.42f), new Vector3(4.35f, 0f, 0f), 8);

            Deck = new GameObject("Cales").transform;
            Deck.SetParent(t, false);

            // Marquage des emplacements de cale.
            for (int s = 0; s < Core.GameRules.ShipColumns * Core.GameRules.ShipRows; s++)
            {
                SpriteFactory.Make("Cale" + s, Deck, SpriteFactory.Ring, new Color(1f, 1f, 1f, 0.22f),
                    BoardLayout.ContainerSize + new Vector2(0.1f, 0.1f), BoardLayout.ShipSlotLocal(s), 7);
            }

            // Sillage (visible pendant les déplacements).
            wake = SpriteFactory.Make("Sillage", t, SpriteFactory.SoftDot, new Color(1f, 1f, 1f, 0f),
                new Vector2(3.5f, 2.2f), new Vector3(-5.2f, 0f, 0f), 2);
        }

        /// <summary>Fait entrer le navire (ou le place directement à quai au chargement).</summary>
        public void Arrive(bool instant)
        {
            gameObject.SetActive(true);
            startX = -BoardLayout.ShipOffscreenX;
            endX = BoardLayout.ShipDock.x;
            timer = instant ? TravelTime : 0f;
            Current = instant ? State.Docked : State.Arriving;
            transform.position = new Vector3(instant ? endX : startX, BoardLayout.ShipDock.y, 0f);
            if (instant) Arrived?.Invoke();
        }

        public void Leave()
        {
            startX = transform.position.x;
            endX = BoardLayout.ShipOffscreenX;
            timer = 0f;
            Current = State.Leaving;
        }

        void Update()
        {
            if (Current == State.Arriving || Current == State.Leaving)
            {
                timer += Time.deltaTime;
                float k = Mathf.Clamp01(timer / TravelTime);
                // Arrivée : décélération douce ; départ : accélération douce.
                float e = Current == State.Arriving ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * k;
                var p = transform.position;
                p.x = Mathf.Lerp(startX, endX, e);
                transform.position = p;
                var c = wake.color;
                c.a = 0.45f * (Current == State.Arriving ? 1f - k : k);
                wake.color = c;

                if (k >= 1f)
                {
                    if (Current == State.Arriving) { Current = State.Docked; Arrived?.Invoke(); }
                    else { Current = State.Absent; gameObject.SetActive(false); Left?.Invoke(); }
                }
            }
            else if (Current == State.Docked && wake.color.a > 0f)
            {
                // À quai : plus de sillage.
                var c = wake.color;
                c.a = 0f;
                wake.color = c;
            }
        }

        /// <summary>Position monde du conteneur au niveau "tier" d'une cale.</summary>
        public Vector3 SlotWorld(int slot, int tier)
        {
            return Deck.TransformPoint(BoardLayout.ShipSlotLocal(slot) + BoardLayout.TierOffset(tier));
        }
    }
}
