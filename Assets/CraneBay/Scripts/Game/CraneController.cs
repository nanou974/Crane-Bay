// -----------------------------------------------------------------------------
// Crane Bay - Portique de manutention (vue de dessus).
//
// Le portique roule sur deux rails verticaux (bords de l'écran) ; le chariot
// se déplace le long de la poutre. Il couvre donc tout le terminal : navire,
// parc, camions et trains.
// Un mouvement = une "mission" : aller à la source, descendre, saisir,
// remonter, aller à la destination, descendre, déposer, remonter.
// La vitesse dépend de l'amélioration "Vitesse grue" (et du turbo).
// Les missions peuvent être mises en file (3 maximum) pour enchaîner les ordres.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class CraneController : MonoBehaviour
    {
        public const int MaxQueue = 3;
        const float BaseSpeed = 5.5f;      // unités/seconde au niveau 0
        const float BaseSmooth = 0.16f;    // lissage de l'accélération
        const float BaseHoistTime = 0.32f; // durée d'une descente/montée

        enum Phase { Idle, ToSource, Lower, Raise, ToDest, LowerDrop, RaiseDrop }

        public struct Job
        {
            public Location from;
            public Location to;
        }

        GameManager game;
        Transform bridge;
        Transform trolley;
        Transform hook;
        SpriteRenderer spreader;

        Vector2 pos;       // x = chariot, y = portique
        Vector2 vel;
        float hoist;       // 0 = haut, 1 = bas
        Phase phase = Phase.Idle;
        Job current;
        readonly Queue<Job> queue = new Queue<Job>();

        public ContainerView Carried { get; private set; }
        public bool CarriedFromShip => Carried != null && current.from.kind == LocationKind.Ship;
        /// <summary>Origine du conteneur porté (pour la sauvegarde en cours de mouvement).</summary>
        public Location CarriedFrom => current.from;
        public bool Busy => phase != Phase.Idle || queue.Count > 0;
        public int Pending => queue.Count + (phase != Phase.Idle ? 1 : 0);

        public void Init(GameManager gm)
        {
            game = gm;
            Build();
            pos = BoardLayout.CraneRest;
            ApplyTransforms();
        }

        void Build()
        {
            var t = transform;
            float legX = BoardLayout.CraneLegX;

            bridge = new GameObject("Portique").transform;
            bridge.SetParent(t, false);
            // Ombre de la poutre (douce, décalée) pour donner du relief.
            SpriteFactory.Make("OmbrePoutre", bridge, SpriteFactory.Shadow, new Color(0, 0, 0, 0.22f), new Vector2(10.6f, 0.75f), new Vector3(0.25f, -0.35f, 0f), 39);
            foreach (var sx in new[] { -1f, 1f })
            {
                SpriteFactory.Make("Pied", bridge, SpriteFactory.Rounded, Palette.CraneDark, new Vector2(0.42f, 1.15f), new Vector3(sx * legX, 0f, 0f), 40);
            }
            SpriteFactory.Make("Poutre", bridge, SpriteFactory.Rounded, Palette.Crane, new Vector2(legX * 2f + 0.2f, 0.34f), Vector3.zero, 41);

            trolley = new GameObject("Chariot").transform;
            trolley.SetParent(bridge, false);
            hook = new GameObject("Crochet").transform;
            hook.SetParent(trolley, false);
            spreader = SpriteFactory.Make("Palonnier", trolley, SpriteFactory.Ring, Palette.Trolley, BoardLayout.ContainerSize + new Vector2(0.12f, 0.12f), Vector3.zero, 47);
            SpriteFactory.Make("Chariot", trolley, SpriteFactory.Rounded, Palette.Trolley, new Vector2(0.78f, 0.56f), Vector3.zero, 48);
            SpriteFactory.Make("Cabine", trolley, SpriteFactory.RoundedSmall, Palette.Window, new Vector2(0.26f, 0.2f), new Vector3(0.16f, 0.1f, 0f), 49);
        }

        /// <summary>Ajoute une mission. Renvoie false si la file est pleine.</summary>
        public bool Enqueue(Location from, Location to)
        {
            if (Pending >= MaxQueue + 1) return false;
            queue.Enqueue(new Job { from = from, to = to });
            return true;
        }

        /// <summary>Annule les ordres en attente (la mission en cours se termine normalement).</summary>
        public void CancelPending() => queue.Clear();

        float Mult => game.CraneSpeedMultiplier;

        void Update()
        {
            if (game == null) return;
            float dt = Time.deltaTime;
            float mult = Mult;

            switch (phase)
            {
                case Phase.Idle:
                    if (queue.Count > 0) StartNext();
                    break;

                case Phase.ToSource:
                    if (!game.CanPickNow(current.from)) { Abort("Ce conteneur n'est plus accessible"); break; }
                    if (MoveTo(game.PickPoint(current.from), dt, mult)) phase = Phase.Lower;
                    break;

                case Phase.Lower:
                    hoist += dt / (BaseHoistTime / mult);
                    if (hoist >= 1f)
                    {
                        hoist = 1f;
                        Carried = game.CraneGrab(current.from, hook);
                        if (Carried == null) { Abort("Impossible de saisir ce conteneur"); break; }
                        phase = Phase.Raise;
                    }
                    break;

                case Phase.Raise:
                    hoist -= dt / (BaseHoistTime / mult);
                    if (hoist <= 0f) { hoist = 0f; phase = Phase.ToDest; }
                    break;

                case Phase.ToDest:
                    // La destination a pu devenir invalide (camion reparti...) : on se replie.
                    if (!game.CanDropNow(current.to, Carried.Info, current.from))
                        current.to = game.FallbackFor(Carried.Info, current.from, pos);
                    if (MoveTo(game.DropPoint(current.to), dt, mult)) phase = Phase.LowerDrop;
                    break;

                case Phase.LowerDrop:
                    hoist += dt / (BaseHoistTime / mult);
                    if (hoist >= 1f)
                    {
                        hoist = 1f;
                        if (!game.CanDropNow(current.to, Carried.Info, current.from))
                        {
                            current.to = game.FallbackFor(Carried.Info, current.from, pos);
                            phase = Phase.Raise; // remonte puis repart vers le repli
                            break;
                        }
                        game.CraneRelease(current.to, current.from, Carried);
                        Carried = null;
                        phase = Phase.RaiseDrop;
                    }
                    break;

                case Phase.RaiseDrop:
                    hoist -= dt / (BaseHoistTime / mult);
                    if (hoist <= 0f) { hoist = 0f; phase = Phase.Idle; }
                    break;
            }

            ApplyTransforms();
        }

        void StartNext()
        {
            current = queue.Dequeue();
            if (!game.CanStartJob(current.from, current.to))
            {
                game.Feedback(Sfx.Error, "Mouvement impossible");
                phase = Phase.Idle;
                return;
            }
            phase = Phase.ToSource;
        }

        void Abort(string message)
        {
            // Appelé uniquement avant la saisie : la grue remonte à vide puis passe à la suite.
            game.Feedback(Sfx.Error, message);
            phase = hoist > 0f ? Phase.RaiseDrop : Phase.Idle;
        }

        /// <summary>Déplacement fluide (accélération/décélération) vers la cible. True si arrivé.</summary>
        bool MoveTo(Vector3 target, float dt, float mult)
        {
            var t2 = new Vector2(target.x, target.y);
            pos = Vector2.SmoothDamp(pos, t2, ref vel, BaseSmooth / Mathf.Sqrt(mult), BaseSpeed * mult, dt);
            if ((pos - t2).sqrMagnitude < 0.0009f)
            {
                pos = t2;
                vel = Vector2.zero;
                return true;
            }
            return false;
        }

        void ApplyTransforms()
        {
            bridge.position = new Vector3(0f, pos.y, 0f);
            trolley.localPosition = new Vector3(Mathf.Clamp(pos.x, -BoardLayout.CraneLegX + 0.5f, BoardLayout.CraneLegX - 0.5f), 0f, 0f);
            // Plus le palonnier descend, plus il paraît petit (il s'éloigne de la caméra).
            float s = 1f - 0.12f * hoist;
            spreader.transform.localScale = new Vector3(s, s, 1f);
            if (Carried != null) Carried.SetLift(1f - hoist);
        }
    }
}
