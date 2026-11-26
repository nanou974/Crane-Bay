// -----------------------------------------------------------------------------
// Crane Bay - Grues (vue de dessus).
//
// Deux modèles partagent ce script :
//  - le PORTIQUE PRINCIPAL : poutre horizontale qui roule sur deux rails
//    verticaux (bords de l'écran) ; il couvre tout le terminal, navire compris ;
//  - la 2e GRUE (achetable) : portique de parc à poutre verticale qui roule sur
//    deux rails horizontaux ; il dessert le parc, la route et la voie ferrée.
//    Il est plus bas que le portique principal : les deux se croisent sans
//    se gêner (comme dans un vrai terminal).
// Un mouvement = une "mission" : aller à la source, descendre, saisir,
// remonter, aller à la destination, descendre, déposer, remonter.
// Pendant une mission, la source et la destination sont réservées pour que
// l'autre grue ne vienne pas y toucher en même temps.
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

        Vector2 pos;       // position du crochet (x, y)
        Vector2 vel;
        float hoist;       // 0 = haut, 1 = bas
        Phase phase = Phase.Idle;
        Job current;
        readonly List<Job> queue = new List<Job>();

        /// <summary>true = portique principal (poutre horizontale) ; false = 2e grue (poutre verticale).</summary>
        public bool IsMain { get; private set; }
        public ContainerView Carried { get; private set; }
        public bool CarriedFromShip => Carried != null && current.from.kind == LocationKind.Ship;
        /// <summary>Origine du conteneur porté (pour la sauvegarde en cours de mouvement).</summary>
        public Location CarriedFrom => current.from;
        public bool Busy => phase != Phase.Idle || queue.Count > 0;
        public int Pending => queue.Count + (phase != Phase.Idle ? 1 : 0);
        public bool IsWorking => phase != Phase.Idle;
        public Job Current => current;

        public void Init(GameManager gm, bool main)
        {
            game = gm;
            IsMain = main;
            if (main) BuildMain(); else BuildSecond();
            pos = main ? BoardLayout.CraneRest : BoardLayout.SecondCraneRest;
            ApplyTransforms();
        }

        void BuildMain()
        {
            var t = transform;
            float legX = BoardLayout.CraneLegX;
            bridge = new GameObject("Portique").transform;
            bridge.SetParent(t, false);
            SpriteFactory.Make("OmbrePoutre", bridge, SpriteFactory.Shadow, new Color(0, 0, 0, 0.22f), new Vector2(10.6f, 0.75f), new Vector3(0.25f, -0.35f, 0f), 39);
            foreach (var sx in new[] { -1f, 1f })
                SpriteFactory.Make("Pied", bridge, SpriteFactory.Rounded, Palette.CraneDark, new Vector2(0.42f, 1.15f), new Vector3(sx * legX, 0f, 0f), 40);
            SpriteFactory.Make("Poutre", bridge, SpriteFactory.Rounded, Palette.Crane, new Vector2(legX * 2f + 0.2f, 0.34f), Vector3.zero, 41);
            BuildTrolley(Palette.Trolley, 47);
        }

        void BuildSecond()
        {
            var t = transform;
            float top = BoardLayout.SecondCraneTopY, bottom = BoardLayout.SecondCraneBottomY;
            float mid = (top + bottom) * 0.5f, half = (top - bottom) * 0.5f;
            bridge = new GameObject("PortiqueParc").transform;
            bridge.SetParent(t, false);
            // Repère : x = position du portique, y = 0 ; la poutre s'étend de bottom à top.
            SpriteFactory.Make("OmbrePoutre", bridge, SpriteFactory.Shadow, new Color(0, 0, 0, 0.18f), new Vector2(0.7f, half * 2f + 0.4f), new Vector3(0.2f, mid - 0.25f, 0f), 33);
            foreach (var y in new[] { top, bottom })
                SpriteFactory.Make("Pied", bridge, SpriteFactory.Rounded, new Color(0.2f, 0.55f, 0.9f), new Vector2(1.0f, 0.36f), new Vector3(0f, y, 0f), 34);
            SpriteFactory.Make("Poutre", bridge, SpriteFactory.Rounded, new Color(0.35f, 0.72f, 1f), new Vector2(0.28f, half * 2f), new Vector3(0f, mid, 0f), 35);
            BuildTrolley(new Color(0.15f, 0.45f, 0.85f), 36);
        }

        void BuildTrolley(Color color, int order)
        {
            trolley = new GameObject("Chariot").transform;
            trolley.SetParent(bridge, false);
            hook = new GameObject("Crochet").transform;
            hook.SetParent(trolley, false);
            spreader = SpriteFactory.Make("Palonnier", trolley, SpriteFactory.Ring, color, BoardLayout.ContainerSize + new Vector2(0.12f, 0.12f), Vector3.zero, order);
            SpriteFactory.Make("Chariot", trolley, SpriteFactory.Rounded, color, new Vector2(0.7f, 0.5f), Vector3.zero, order + 1);
            SpriteFactory.Make("Cabine", trolley, SpriteFactory.RoundedSmall, Palette.Window, new Vector2(0.24f, 0.18f), new Vector3(0.14f, 0.09f, 0f), order + 2);
        }

        /// <summary>Cette grue peut-elle atteindre cet emplacement ? (la 2e grue ne va pas au navire)</summary>
        public bool CanReach(Location loc) => IsMain || loc.kind != LocationKind.Ship;

        /// <summary>Ajoute une mission. Renvoie false si la file est pleine.</summary>
        public bool Enqueue(Location from, Location to)
        {
            if (Pending >= MaxQueue + 1) return false;
            queue.Add(new Job { from = from, to = to });
            return true;
        }

        /// <summary>Annule les ordres en attente (la mission en cours se termine normalement).</summary>
        public void CancelPending() => queue.Clear();

        void Update()
        {
            if (game == null) return;
            float dt = Time.deltaTime;
            float mult = game.CraneSpeedMultiplier;

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
                    if (!game.CanDropNow(current.to, Carried.Info, current.from, this))
                        Retarget();
                    if (MoveTo(game.DropPoint(current.to), dt, mult)) phase = Phase.LowerDrop;
                    break;

                case Phase.LowerDrop:
                    hoist += dt / (BaseHoistTime / mult);
                    if (hoist >= 1f)
                    {
                        hoist = 1f;
                        if (!game.CanDropNow(current.to, Carried.Info, current.from, this))
                        {
                            Retarget();
                            phase = Phase.Raise; // remonte puis repart vers le repli
                            break;
                        }
                        var released = Carried;
                        Carried = null;
                        game.CraneRelease(current.to, current.from, released);
                        phase = Phase.RaiseDrop;
                    }
                    break;

                case Phase.RaiseDrop:
                    hoist -= dt / (BaseHoistTime / mult);
                    if (hoist <= 0f) { hoist = 0f; Finish(); }
                    break;
            }

            ApplyTransforms();
        }

        void StartNext()
        {
            // Première mission dont les emplacements ne sont pas réservés par l'autre grue.
            for (int i = 0; i < queue.Count; i++)
            {
                var job = queue[i];
                if (game.IsLockedByOther(job.from, this) || game.IsLockedByOther(job.to, this)) continue;
                queue.RemoveAt(i);
                if (!game.CanStartJob(job.from, job.to, this))
                {
                    game.Feedback(Sfx.Error, "Mouvement impossible");
                    return;
                }
                current = job;
                game.Lock(current.from, this);
                game.Lock(current.to, this);
                phase = Phase.ToSource;
                return;
            }
        }

        void Retarget()
        {
            game.Unlock(current.to, this);
            current.to = game.FallbackFor(Carried.Info, current.from, pos, this);
            game.Lock(current.to, this);
        }

        void Finish()
        {
            game.Unlock(current.from, this);
            game.Unlock(current.to, this);
            phase = Phase.Idle;
        }

        void Abort(string message)
        {
            // Appelé uniquement avant la saisie : la grue remonte à vide puis passe à la suite.
            game.Feedback(Sfx.Error, message);
            if (hoist > 0f) phase = Phase.RaiseDrop;
            else Finish();
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
            if (IsMain)
            {
                bridge.position = new Vector3(0f, pos.y, 0f);
                trolley.localPosition = new Vector3(Mathf.Clamp(pos.x, -BoardLayout.CraneLegX + 0.5f, BoardLayout.CraneLegX - 0.5f), 0f, 0f);
            }
            else
            {
                bridge.position = new Vector3(pos.x, 0f, 0f);
                trolley.localPosition = new Vector3(0f, Mathf.Clamp(pos.y, BoardLayout.SecondCraneBottomY + 0.3f, BoardLayout.SecondCraneTopY - 0.3f), 0f);
            }
            // Plus le palonnier descend, plus il paraît petit (il s'éloigne de la caméra).
            float s = 1f - 0.12f * hoist;
            spreader.transform.localScale = new Vector3(s, s, 1f);
            if (Carried != null) Carried.SetLift(1f - hoist);
        }
    }
}
