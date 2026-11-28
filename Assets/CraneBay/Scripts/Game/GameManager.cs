// -----------------------------------------------------------------------------
// Crane Bay - Chef d'orchestre du jeu.
//
// Relie le modèle logique (PortModel, sans affichage) aux vues : décor,
// ambiance (nuit, météo), navire, véhicules, grues, interface, sons,
// feu d'artifice, sauvegarde.
// Toute la scène est construite par code au démarrage : la scène Unity ne
// contient qu'un objet avec ce composant (et même sans lui, Bootstrap le crée).
//
// Boucle de jeu :
//  1. un navire arrive et apporte une série de conteneurs ;
//  2. le joueur les range dans le parc avec la grue (règles d'empilage) ;
//  3. camions / trains (annoncés à l'avance) demandent une couleur précise ;
//  4. chaque conteneur livré rapporte argent et XP ; les contrats du jour
//     donnent des primes ; un niveau franchi remet le terminal à zéro ;
//  5. l'argent achète des améliorations (vitesse, attentes, parc, 2e grue).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PortModel Model { get; private set; }

        const float AutosaveInterval = 10f;
        const float HudRefreshInterval = 0.25f;
        const float ReeferRefreshInterval = 0.5f;

        readonly System.Random rng = new System.Random();
        readonly Dictionary<int, ContainerView> containerViews = new Dictionary<int, ContainerView>();
        readonly Dictionary<Vehicle, VehicleView> vehicleViews = new Dictionary<Vehicle, VehicleView>();
        readonly List<SpriteRenderer> hintPool = new List<SpriteRenderer>();
        readonly List<CraneController> cranes = new List<CraneController>();
        readonly Dictionary<Location, CraneController> locks = new Dictionary<Location, CraneController>();

        Camera cam;
        Transform world, yardRoot, vehiclesRoot;
        WorldBuilder decor;
        ShipView ship;
        CraneController mainCrane;
        Hud hud;
        AudioManager audioManager;
        FloatingText floating;
        Fireworks fireworks;

        Location selected = Location.None;
        SpriteRenderer selectRing;
        ContainerView dragGhost;
        ContainerView releasingView; // conteneur en cours de dépose (protégé pendant une remise à zéro)

        float autosaveTimer, hudTimer, reeferTimer, contractTimer, saveSoonTimer = -1f;
        int lastScreenW, lastScreenH;
        PortTheme appliedTheme = (PortTheme)(-1);
        bool levelPendingShown;

        public float CraneSpeedMultiplier => Model.CraneMultiplier(TimeUtil.NowUnix);
        public bool SoundMuted => audioManager.Muted;
        public string SoundLabel => audioManager.ModeLabel;

        // =====================================================================
        // Démarrage
        // =====================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Mobile : 60 FPS visés, écran toujours allumé pendant la partie.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            double now = TimeUtil.NowUnix;
            Model = new PortModel(SaveSystem.Load(), now);
            Model.EnsureContracts(now, rng);

            SpriteFactory.Ensure();
            SetupCamera();

            world = new GameObject("Terminal").transform;
            decor = world.gameObject.AddComponent<WorldBuilder>();
            decor.Build();
            yardRoot = new GameObject("Parc").transform;
            yardRoot.SetParent(world, false);
            vehiclesRoot = new GameObject("Vehicules").transform;
            vehiclesRoot.SetParent(world, false);

            ship = ShipView.Create(world);
            ship.Arrived += () => Feedback(null, "Un navire est à quai : déchargez-le !");

            audioManager = gameObject.AddComponent<AudioManager>();

            mainCrane = CreateCrane(true);
            if (Model.HasSecondCrane) CreateCrane(false);

            floating = new GameObject("TextesFlottants").AddComponent<FloatingText>();
            floating.transform.SetParent(world, false);

            var ambience = new GameObject("Ambiance").AddComponent<AmbienceView>();
            ambience.transform.SetParent(world, false);
            ambience.Build(decor.lampPositions);

            fireworks = new GameObject("FeuDArtifice").AddComponent<Fireworks>();
            fireworks.transform.SetParent(world, false);
            fireworks.Init(audioManager);

            hud = new GameObject("HUD").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Init(this);

            gameObject.AddComponent<InputController>().Init(this, cam);

            selectRing = SpriteFactory.Make("Selection", world, SpriteFactory.Ring, Palette.Highlight,
                BoardLayout.ContainerSize + new Vector2(0.28f, 0.28f), Vector3.zero, 30);
            selectRing.gameObject.SetActive(false);

            // Événements du modèle -> vues.
            Model.ShipArrived += OnShipArrived;
            Model.ShipDeparting += () => ship.Leave();
            Model.VehicleSpawned += OnVehicleSpawned;
            Model.VehicleLeaving += OnVehicleLeaving;
            Model.LevelUp += OnLevelUp;
            Model.TerminalReset += OnTerminalReset;
            Model.ContractCompleted += c => Feedback(Sfx.Coin, "Contrat rempli : " + c.Describe() + "\n+" + Hud.FormatMoney((long)System.Math.Round(c.rewardMoney * GameRules.IncomeMultiplier(Model.Level(UpgradeType.Revenus)))) + "  +" + c.rewardXp + " XP");
            Model.RecordBroken += msg => Feedback(Sfx.Upgrade, msg);

            // État chargé : parc et, éventuellement, navire déjà à quai.
            RebuildYardViews();
            if (Model.Data.shipDocked)
            {
                BuildShipCargoViews();
                ship.Arrive(true);
            }
            ApplyProgressVisuals();
            hud.Refresh(now);
        }

        CraneController CreateCrane(bool main)
        {
            var c = new GameObject(main ? "Grue" : "Grue2").AddComponent<CraneController>();
            c.transform.SetParent(world, false);
            c.Init(this, main);
            cranes.Add(c);
            return c;
        }

        /// <summary>Décor qui dépend de la progression : thème du port, parc débloqué, rails de la 2e grue.</summary>
        void ApplyProgressVisuals()
        {
            var theme = Model.Theme;
            if (theme != appliedTheme)
            {
                appliedTheme = theme;
                decor.ApplyTheme(theme);
                cam.backgroundColor = Palette.Themes[(int)theme].clear;
            }
            decor.SetUnlockedRows(Model.YardRowsUnlocked);
            decor.SetSecondCrane(Model.HasSecondCrane);
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Grass;
            cam.transform.position = new Vector3(0f, 0.4f, -10f);
            cam.transform.rotation = Quaternion.identity;
            FitCamera();
        }

        /// <summary>Cadrage : toute la largeur du terminal est toujours visible (portrait).</summary>
        void FitCamera()
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.orthographicSize = Mathf.Max(BoardLayout.MinHalfHeight, BoardLayout.HalfWidth / aspect);
        }

        // =====================================================================
        // Boucle principale
        // =====================================================================

        // =====================================================================
        // Pause
        // =====================================================================

        public bool Paused { get; private set; }

        /// <summary>
        /// Met le jeu en pause ou le relance. Pendant la pause, les animations
        /// sont figées (timeScale = 0) et les échéances du modèle (navire,
        /// patience des véhicules, turbo) sont décalées à chaque image, pour
        /// reprendre exactement où on en était. Les coffres continuent à se recharger.
        /// </summary>
        public void TogglePause()
        {
            Paused = !Paused;
            Time.timeScale = Paused ? 0f : 1f;
            if (Paused) { ClearSelection(); EndDragGhost(); }
            hud.SetPaused(Paused);
            audioManager.Play(Sfx.Select);
        }

        // =====================================================================
        // Mini-jeu de détente (le port est en pause pendant le puzzle)
        // =====================================================================

        public void OpenPuzzle()
        {
            if (hud.PuzzleOpen) return;
            if (!Paused)
            {
                Paused = true;
                Time.timeScale = 0f;
                ClearSelection();
                EndDragGhost();
            }
            else hud.SetPaused(false); // masque l'écran de pause, la pause continue
            hud.ShowPuzzle();
            audioManager.Play(Sfx.Select);
        }

        public void ClosePuzzle()
        {
            if (!hud.PuzzleOpen) return;
            hud.HidePuzzle();
            Paused = false;
            Time.timeScale = 1f;
            hud.SetPaused(false);
            audioManager.Play(Sfx.Select);
            SaveNow();
        }

        /// <summary>Puzzle résolu : prime, son, sauvegarde. Renvoie la prime.</summary>
        public long CompletePuzzle(bool perfect)
        {
            long reward = Model.CompletePuzzle(perfect);
            audioManager.Play(perfect ? Sfx.Fanfare : Sfx.Coin);
            SaveNow();
            return reward;
        }

        public void PlayUi(Sfx sfx) => audioManager.Play(sfx, 0.7f);

        void EndDragGhost()
        {
            if (dragGhost != null) { Destroy(dragGhost.gameObject); dragGhost = null; }
        }

        void Update()
        {
            double now = TimeUtil.NowUnix;
            float dt = Time.deltaTime;
            if (Screen.width != lastScreenW || Screen.height != lastScreenH) FitCamera();

            if (Paused)
            {
                Model.ShiftTimers(Time.unscaledDeltaTime);
                hudTimer -= Time.unscaledDeltaTime;
                if (hudTimer <= 0f) { hudTimer = HudRefreshInterval; hud.Refresh(now); }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (hud.PuzzleOpen) ClosePuzzle();
                    else TogglePause();
                }
                return;
            }

            // Navires : arrivée selon l'horloge réelle (fonctionne aussi après une fermeture).
            Model.UpdateShip(now, rng);
            if (Model.ShipNeedsDeparture && ship.IsDocked && !mainCrane.CarriedFromShip)
            {
                Model.DepartShip(now);
                Feedback(Sfx.Horn, "Navire déchargé ! Prochain dans " + ChestLogic.Format(Model.SecondsUntilShip(now)));
                SaveSoon();
            }

            // Camions et trains.
            Model.UpdateVehicles(now, rng);
            foreach (var kv in vehicleViews)
            {
                kv.Value.SetPatience(PortModel.PatienceRatio(kv.Key, now));
                kv.Value.SetFast(PortModel.FastBonusActive(kv.Key, now));
            }

            // Barre d'XP pleine : le niveau attend que les véhicules présents soient servis.
            bool pending = Model.LevelUpPending;
            if (pending && !levelPendingShown)
                Feedback(Sfx.Upgrade, "Barre d'XP pleine ! Servez les camions et trains présents pour passer au niveau " + (Model.PlayerLevel + 1));
            levelPendingShown = pending;

            // Frigorifiques débranchés : la valeur affichée baisse.
            Model.UpdateReefers(dt);
            reeferTimer -= dt;
            if (reeferTimer <= 0f)
            {
                reeferTimer = ReeferRefreshInterval;
                foreach (var v in containerViews.Values) if (v != null && v.Info.reefer) v.RefreshValue();
            }

            // Contrats : renouvellement au changement de jour.
            contractTimer -= dt;
            if (contractTimer <= 0f)
            {
                contractTimer = 1f;
                if (Model.EnsureContracts(now, rng)) Feedback(Sfx.Chest, "Nouveaux contrats du jour disponibles !");
            }

            UpdateSelectionVisuals();

            hudTimer -= dt;
            if (hudTimer <= 0f) { hudTimer = HudRefreshInterval; hud.Refresh(now); }

            autosaveTimer += dt;
            if (autosaveTimer >= AutosaveInterval) SaveNow();
            if (saveSoonTimer >= 0f)
            {
                saveSoonTimer -= dt;
                if (saveSoonTimer < 0f) SaveNow();
            }

            // Bouton "retour" Android : ferme le panneau, annule la sélection,
            // sinon met en pause (le menu pause propose de quitter).
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (hud.PanelOpen) hud.ClosePanel();
                else if (!selected.IsNone) ClearSelection();
                else TogglePause();
            }
        }

        // =====================================================================
        // Navire
        // =====================================================================

        void OnShipArrived()
        {
            BuildShipCargoViews();
            ship.Arrive(false);
            audioManager.Play(Sfx.Horn, 0.8f);
            SaveSoon();
        }

        void BuildShipCargoViews()
        {
            var g = Model.Data.ship;
            for (int s = 0; s < g.SlotCount; s++)
                for (int tier = 0; tier < g.Height(s); tier++)
                {
                    var info = g.At(s, tier);
                    var v = GetOrCreateView(info, ship.Deck);
                    v.transform.localPosition = BoardLayout.ShipSlotLocal(s) + BoardLayout.TierOffset(tier);
                    v.SetTier(tier);
                }
        }

        void RebuildYardViews()
        {
            var g = Model.Data.yard;
            for (int s = 0; s < g.SlotCount; s++)
                for (int tier = 0; tier < g.Height(s); tier++)
                    PlaceInYard(GetOrCreateView(g.At(s, tier), yardRoot), s, tier);
        }

        ContainerView GetOrCreateView(ContainerInfo info, Transform parent)
        {
            if (!containerViews.TryGetValue(info.id, out var v) || v == null)
            {
                v = ContainerView.Create(parent, info);
                containerViews[info.id] = v;
            }
            else v.transform.SetParent(parent, false);
            return v;
        }

        void PlaceInYard(ContainerView v, int slot, int tier)
        {
            v.transform.SetParent(yardRoot, false);
            v.transform.localPosition = BoardLayout.YardSlotCenter(slot) + BoardLayout.TierOffset(tier);
            v.SetTier(tier);
            v.RefreshValue();
        }

        // =====================================================================
        // Véhicules
        // =====================================================================

        void OnVehicleSpawned(Vehicle v)
        {
            var view = VehicleView.Create(vehiclesRoot, v, rng);
            vehicleViews[v] = view;
            view.OnDocked = vv =>
            {
                Model.MarkArrived(vv.Model, TimeUtil.NowUnix);
                audioManager.Play(Sfx.Honk, 0.6f);
            };
            view.OnGone = vv =>
            {
                Model.RemoveVehicle(vv.Model);
                vehicleViews.Remove(vv.Model);
                foreach (var cv in vv.Loaded) if (cv != null) containerViews.Remove(cv.Info.id);
                Destroy(vv.gameObject);
            };
        }

        void OnVehicleLeaving(Vehicle v)
        {
            if (!vehicleViews.TryGetValue(v, out var view)) return;
            bool complete = v.AllFilled;
            view.Leave(complete ? 0.45f : 0f);
            if (!complete)
            {
                Feedback(Sfx.Error, v.kind == VehicleKind.Train
                    ? "Le train est parti sans attendre la fin du chargement…"
                    : "Un camion a perdu patience et repart à vide…");
            }
            else if (v.kind == VehicleKind.Train)
            {
                Feedback(Sfx.Coin, "Train complet ! +" + Hud.FormatMoney(v.earned));
            }
        }

        /// <summary>Destination véhicule encore en approche (la grue doit attendre avant de déposer).</summary>
        public bool IsArriving(Location to) =>
            to.IsVehicle && TryGetVehicleView(to, out var view, out _) && view.Arriving;

        bool TryGetVehicleView(Location loc, out VehicleView view, out int cargoIndex)
        {
            view = null; cargoIndex = 0;
            if (!Model.TryGetDemand(loc, out var v, out _)) return false;
            if (!vehicleViews.TryGetValue(v, out view)) return false;
            cargoIndex = loc.kind == LocationKind.Wagon ? loc.index : 0;
            return true;
        }

        // =====================================================================
        // Niveaux du joueur
        // =====================================================================

        void OnLevelUp(LevelUpInfo info)
        {
            // Bilan du niveau : étoiles, prime (multipliée par les étoiles), turbo offert.
            string details = info.delivered + " livraisons, " + info.fast + " rapides, " + info.missed + " véhicule(s) raté(s)";
            details += "\nPrime : +" + Hud.FormatMoney(info.reward);
            if (info.stars >= 2) details += " (x" + LevelRules.StarFactor(info.stars).ToString("0.#") + ")";
            if (info.turboSeconds > 0) details += "  +  TURBO " + ChestLogic.Format(info.turboSeconds);
            if (!string.IsNullOrEmpty(info.unlocks)) details += "\nNouveau : " + info.unlocks;
            hud.ShowLevelBanner(info.newLevel, details, info.stars);
            fireworks.Play(8);
            audioManager.Play(Sfx.Fanfare);
            ApplyProgressVisuals();
            hud.Refresh(TimeUtil.NowUnix);
            SaveSoon();
        }

        /// <summary>
        /// Passage de niveau : le modèle a vidé le parc et le navire. On efface
        /// les conteneurs affichés, on annule les ordres des grues, le navire
        /// et les véhicules s'en vont. Le conteneur qui vient d'être livré reste
        /// sur son véhicule (il part avec lui).
        /// </summary>
        void OnTerminalReset()
        {
            foreach (var c in cranes) c.CancelPending();
            ClearSelection();
            if (dragGhost != null) { Destroy(dragGhost.gameObject); dragGhost = null; }

            var keep = new HashSet<int>();
            foreach (var view in vehicleViews.Values)
                foreach (var cv in view.Loaded) if (cv != null) keep.Add(cv.Info.id);
            var toRemove = new List<int>();
            foreach (var kv in containerViews)
            {
                if (keep.Contains(kv.Key)) continue;
                bool protectedView = kv.Value == releasingView;
                foreach (var c in cranes) if (kv.Value == c.Carried) protectedView = true;
                if (kv.Value != null && !protectedView) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove) containerViews.Remove(id);

            if (ship.Current == ShipView.State.Docked || ship.Current == ShipView.State.Arriving) ship.Leave();
            foreach (var view in vehicleViews.Values) view.Leave(0.6f);
            SaveSoon();
        }

        // =====================================================================
        // Services pour les grues (réservations, points de prise et de dépose)
        // =====================================================================

        public bool IsLockedByOther(Location loc, CraneController crane) =>
            !loc.IsNone && locks.TryGetValue(loc, out var owner) && owner != crane;

        public void Lock(Location loc, CraneController crane)
        {
            if (!loc.IsNone) locks[loc] = crane;
        }

        public void Unlock(Location loc, CraneController crane)
        {
            if (locks.TryGetValue(loc, out var owner) && owner == crane) locks.Remove(loc);
        }

        /// <summary>Peut-on saisir à cet endroit maintenant ? (navire : seulement à quai)</summary>
        public bool CanPickNow(Location from)
        {
            if (from.kind == LocationKind.Ship && !ship.IsDocked) return false;
            return Model.CanPickFrom(from);
        }

        public bool CanDropNow(Location to, ContainerInfo c, Location from, CraneController crane = null)
        {
            if (to.IsNone || c == null) return false;
            if (crane != null && (IsLockedByOther(to, crane) || !crane.CanReach(to))) return false;
            // Repli exceptionnel : retour dans la cale d'origine.
            if (to.kind == LocationKind.Ship)
                return to == from && ship.IsDocked && Model.Data.shipDocked && Model.Data.ship.CanPlace(to.index);
            if (to.IsVehicle)
            {
                // Un véhicule en approche est déjà une destination valide : la grue
                // attendra qu'il soit à quai pour déposer.
                if (!TryGetVehicleView(to, out var view, out _) || !(view.Docked || view.Arriving)) return false;
                return Model.CanDropAt(to, c, from, allowArriving: true);
            }
            return Model.CanDropAt(to, c, from);
        }

        public bool CanStartJob(Location from, Location to, CraneController crane = null)
        {
            if (crane != null && !crane.CanReach(from)) return false;
            if (!CanPickNow(from)) return false;
            return CanDropNow(to, Model.PeekTop(from), from, crane);
        }

        /// <summary>Point de prise : le conteneur du dessus.</summary>
        public Vector3 PickPoint(Location from)
        {
            var g = Model.GridOf(from.kind);
            int tier = Mathf.Max(0, (g?.Height(from.index) ?? 1) - 1);
            return StackPoint(from, tier);
        }

        /// <summary>Point de dépose : au-dessus de la pile, ou sur le véhicule.</summary>
        public Vector3 DropPoint(Location to)
        {
            if (to.IsVehicle)
            {
                if (TryGetVehicleView(to, out var view, out int idx)) return view.Docked ? view.CargoWorld(idx) : view.CargoDockWorld(idx);
                return new Vector3(0f, BoardLayout.RoadY, 0f);
            }
            if (to.IsNone) return BoardLayout.CraneRest;
            var g = Model.GridOf(to.kind);
            return StackPoint(to, g.Height(to.index));
        }

        Vector3 StackPoint(Location loc, int tier)
        {
            if (loc.kind == LocationKind.Ship) return ship.SlotWorld(loc.index, tier);
            return yardRoot.TransformPoint(BoardLayout.YardSlotCenter(loc.index) + BoardLayout.TierOffset(tier));
        }

        /// <summary>La grue saisit le conteneur du dessus : retiré du modèle, attaché au crochet.</summary>
        public ContainerView CraneGrab(Location from, Transform hook)
        {
            if (!CanPickNow(from)) return null;
            var info = Model.Pick(from);
            if (info == null) return null;
            var view = GetOrCreateView(info, hook);
            view.transform.localPosition = Vector3.zero;
            view.SetCarried();
            view.SetLift(0f);
            audioManager.Play(Sfx.Grab, 0.8f);
            if (selected == from && !Model.CanPickFrom(from)) ClearSelection();
            return view;
        }

        /// <summary>La grue pose le conteneur : parc, véhicule (paiement) ou retour en cale.</summary>
        public void CraneRelease(Location to, Location from, ContainerView view)
        {
            double now = TimeUtil.NowUnix;
            var info = view.Info;
            releasingView = view;
            try
            {
                if (to.kind == LocationKind.Ship)
                {
                    if (Model.ReturnToShip(to.index, info))
                    {
                        int tier = Model.Data.ship.Height(to.index) - 1;
                        view.transform.SetParent(ship.Deck, false);
                        view.transform.localPosition = BoardLayout.ShipSlotLocal(to.index) + BoardLayout.TierOffset(tier);
                        view.SetTier(tier);
                        view.PlayLanding();
                    }
                    audioManager.Play(Sfx.Drop);
                    return;
                }

                if (to.kind == LocationKind.Yard)
                {
                    var res = Model.Drop(to, info, now);
                    if (res.ok)
                    {
                        PlaceInYard(view, to.index, Model.Data.yard.Height(to.index) - 1);
                        view.PlayLanding();
                        audioManager.Play(Sfx.Drop);
                    }
                    SaveSoon();
                    return;
                }

                // Livraison à un véhicule.
                TryGetVehicleView(to, out var vehicleView, out int idx);
                Vector3 at = vehicleView != null ? vehicleView.CargoWorld(idx) : view.transform.position;
                var r = Model.Drop(to, info, now);
                if (r.ok && vehicleView != null)
                {
                    vehicleView.AttachContainer(idx, view);
                    containerViews.Remove(info.id);
                    audioManager.Play(Sfx.Drop, 0.7f);
                    audioManager.Play(Sfx.Coin);
                    floating.Spawn(at + new Vector3(0f, 0.6f, 0f), "+" + Hud.FormatMoney(r.earned), Palette.UiGold);
                    if (r.xp > 0)
                        floating.Spawn(at + new Vector3(0f, 0.05f, 0f), "+" + r.xp + " XP", new Color(0.55f, 0.85f, 1f));
                    if (r.fast)
                        floating.Spawn(at + new Vector3(0f, 1.1f, 0f), "RAPIDE !", new Color(1f, 0.6f, 0.2f));
                    SaveSoon();
                }
            }
            finally { releasingView = null; }
        }

        /// <summary>Destination de repli quand la destination prévue n'est plus valable.</summary>
        public Location FallbackFor(ContainerInfo c, Location from, Vector2 cranePos, CraneController crane)
        {
            var g = Model.Data.yard;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int s = 0; s < g.SlotCount; s++)
            {
                var loc = new Location(LocationKind.Yard, s);
                if (Model.CheckYardPlace(s, c) != PlaceError.Aucune || IsLockedByOther(loc, crane)) continue;
                float d = ((Vector2)BoardLayout.YardSlotCenter(s) - cranePos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            if (best >= 0)
            {
                Feedback(Sfx.Error, "Destination indisponible : conteneur posé dans le parc");
                return new Location(LocationKind.Yard, best);
            }
            if (from.kind == LocationKind.Ship) return from;
            // Dernier recours : remettre sur sa pile d'origine, même hors règles.
            if (from.kind == LocationKind.Yard && g.CanPlace(from.index)) return from;
            return Location.None;
        }

        // =====================================================================
        // Sélection, tap et glisser-déposer
        // =====================================================================

        /// <summary>Quel emplacement se trouve sous ce point du monde ?</summary>
        public Location HitTest(Vector2 p)
        {
            // Véhicules à quai, ou en approche (visés à leur emplacement d'arrêt).
            foreach (var kv in vehicleViews)
            {
                var view = kv.Value;
                if (!view.Docked && !view.Arriving) continue;
                for (int i = 0; i < view.CargoCount; i++)
                    if (BoardLayout.Inside(p, view.Docked ? view.CargoWorld(i) : view.CargoDockWorld(i), view.CargoHitSize))
                        return kv.Key.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, kv.Key.bay);
            }
            // Parc.
            for (int s = 0; s < Model.Data.yard.SlotCount; s++)
                if (BoardLayout.Inside(p, BoardLayout.YardSlotCenter(s) + new Vector3(0, 0.1f, 0), BoardLayout.SlotHitSize))
                    return new Location(LocationKind.Yard, s);
            // Navire.
            if (ship.IsDocked)
                for (int s = 0; s < Model.Data.ship.SlotCount; s++)
                    if (BoardLayout.Inside(p, ship.SlotWorld(s, 0) + new Vector3(0, 0.08f, 0), BoardLayout.SlotHitSize))
                        return new Location(LocationKind.Ship, s);
            return Location.None;
        }

        public bool IsPickable(Location loc) => loc.IsStack && CanPickNow(loc);

        public void OnTap(Location loc)
        {
            if (selected.IsNone)
            {
                if (IsPickable(loc)) Select(loc);
                else if (loc.IsVehicle) Feedback(Sfx.Select, "Choisissez d'abord un conteneur, puis le véhicule");
                return;
            }
            if (loc == selected) { ClearSelection(); return; }
            if (!loc.IsNone && CanQueue(selected, loc)) { Issue(selected, loc); return; }
            if (IsPickable(loc)) { Select(loc); return; }
            Feedback(Sfx.Error, DestinationError(loc, Model.PeekTop(selected)));
            ClearSelection();
        }

        public void BeginDrag(Location from)
        {
            Select(from);
            var top = Model.PeekTop(from);
            if (top == null) return;
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            dragGhost = ContainerView.Create(world, top);
            dragGhost.SetCarried();
            dragGhost.SetAlpha(0.6f);
            dragGhost.name = "FantomeGlisser";
        }

        public void UpdateDrag(Vector2 worldPos)
        {
            if (dragGhost != null) dragGhost.transform.position = new Vector3(worldPos.x, worldPos.y + 0.5f, 0f);
        }

        public void EndDrag(Location from, Location to)
        {
            if (dragGhost != null) { Destroy(dragGhost.gameObject); dragGhost = null; }
            if (to == from) return; // relâché sur place : reste sélectionné (comme un tap)
            if (!to.IsNone && CanQueue(from, to)) Issue(from, to);
            else
            {
                if (!to.IsNone) Feedback(Sfx.Error, DestinationError(to, Model.PeekTop(from)));
                ClearSelection();
            }
        }

        bool CanQueue(Location from, Location to)
        {
            if (to.kind == LocationKind.Ship) return false;
            return CanStartJob(from, to);
        }

        string DestinationError(Location to, ContainerInfo c)
        {
            if (to.kind == LocationKind.Ship) return "On ne recharge pas le navire";
            if (to.kind == LocationKind.Yard && c != null)
            {
                switch (Model.CheckYardPlace(to.index, c))
                {
                    case PlaceError.Verrouille: return "Zone verrouillée : achetez « Agrandir le parc »";
                    case PlaceError.PilePleine: return "Pile pleine (" + Model.YardHeightLimit + " conteneurs max)";
                    case PlaceError.QuaranteSurVingt: return "Un 40 pieds ne se pose pas sur un 20 pieds";
                    case PlaceError.Dangereux: return "Matières dangereuses : jamais à côté d'un autre conteneur dangereux";
                }
            }
            if (to.IsVehicle) return "Ce véhicule attend une autre couleur";
            return "Destination impossible";
        }

        /// <summary>Confie le mouvement à la grue la plus adaptée (la 2e grue ne va pas au navire).</summary>
        void Issue(Location from, Location to)
        {
            CraneController best = null;
            foreach (var c in cranes)
            {
                if (!c.CanReach(from) || !c.CanReach(to)) continue;
                if (c.Pending > CraneController.MaxQueue) continue;
                if (best == null || c.Pending < best.Pending || (c.Pending == best.Pending && !c.IsMain)) best = c;
            }
            if (best != null && best.Enqueue(from, to)) audioManager.Play(Sfx.Select);
            else Feedback(Sfx.Error, "La grue a déjà assez d'ordres en attente");
            ClearSelection();
        }

        void Select(Location loc)
        {
            selected = loc;
            audioManager.Play(Sfx.Select, 0.7f);
        }

        void ClearSelection() => selected = Location.None;

        /// <summary>Anneau de sélection pulsant + indications vertes sur les destinations valides.</summary>
        void UpdateSelectionVisuals()
        {
            if (!selected.IsNone && !IsPickable(selected)) selected = Location.None;

            int used = 0;
            if (selected.IsNone)
            {
                selectRing.gameObject.SetActive(false);
            }
            else
            {
                selectRing.gameObject.SetActive(true);
                selectRing.transform.position = PickPoint(selected);
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 8f);
                selectRing.transform.localScale = new Vector3(pulse, pulse, 1f);

                var top = Model.PeekTop(selected);
                for (int s = 0; s < Model.Data.yard.SlotCount; s++)
                {
                    var to = new Location(LocationKind.Yard, s);
                    if (CanDropNow(to, top, selected)) ShowHint(used++, DropPoint(to), 0.35f);
                }
                foreach (var kv in vehicleViews)
                {
                    var v = kv.Key;
                    for (int i = 0; i < v.demands.Count; i++)
                    {
                        var to = v.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, v.bay);
                        if (CanDropNow(to, top, selected)) ShowHint(used++, DropPoint(to), 0.95f);
                    }
                }
            }
            for (int i = used; i < hintPool.Count; i++) hintPool[i].gameObject.SetActive(false);
        }

        void ShowHint(int index, Vector3 pos, float alpha)
        {
            while (hintPool.Count <= index)
            {
                var sr = SpriteFactory.Make("Indication", world, SpriteFactory.Ring, Palette.ValidTarget,
                    BoardLayout.ContainerSize + new Vector2(0.1f, 0.1f), Vector3.zero, 29);
                hintPool.Add(sr);
            }
            var h = hintPool[index];
            h.gameObject.SetActive(true);
            h.transform.position = pos;
            var c = Palette.ValidTarget;
            c.a = alpha * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f));
            h.color = c;
        }

        // =====================================================================
        // Économie, coffres, réglages
        // =====================================================================

        public void BuyUpgrade(UpgradeType type)
        {
            double now = TimeUtil.NowUnix;
            if (GameRules.IsMaxed(type, Model.Level(type))) { Feedback(Sfx.Error, "Niveau maximum atteint"); return; }
            if (!Model.TryBuy(type))
            {
                Feedback(Sfx.Error, "Pas assez d'argent (" + Hud.FormatMoney(Model.NextUpgradeCost(type)) + ")");
                return;
            }
            ApplyUpgradeEffects(type, now);
        }

        /// <summary>Effets immédiats d'un nouveau niveau d'amélioration (achat ou carte gratuite).</summary>
        void ApplyUpgradeEffects(UpgradeType type, double now)
        {
            // Une attente raccourcie s'applique tout de suite au compte à rebours en cours.
            if (type == UpgradeType.AttenteNavires && !Model.Data.shipDocked)
                Model.Data.nextShipUnix = System.Math.Min(Model.Data.nextShipUnix, now + GameRules.ShipInterval(Model.Level(type)));
            if (type == UpgradeType.AttenteVehicules)
                Model.nextVehicleUnix = System.Math.Min(Model.nextVehicleUnix, now + GameRules.VehicleInterval(Model.Level(type)));
            if (type == UpgradeType.DeuxiemeGrue && cranes.Count < 2)
            {
                CreateCrane(false);
                Feedback(Sfx.Upgrade, "2e grue en service : elle dessert le parc, les camions et les trains !");
            }
            else if (type == UpgradeType.AgrandirParc)
                Feedback(Sfx.Upgrade, "Parc agrandi : " + GameRules.EffectLabel(type, Model.Level(type)));
            else
                Feedback(Sfx.Upgrade, GameRules.UpgradeNames[(int)type] + " : niveau " + Model.Level(type) + " !");

            ApplyProgressVisuals();
            hud.Refresh(now);
            SaveNow();
        }

        /// <summary>Carte au choix de fin de niveau.</summary>
        public void ChooseCard(LevelCard card)
        {
            double now = TimeUtil.NowUnix;
            int upgrade = Model.FreeUpgradeOption();
            if (!Model.ChooseCard(card, now)) return;
            if (card == LevelCard.Amelioration && upgrade >= 0)
            {
                ApplyUpgradeEffects((UpgradeType)upgrade, now); // message, visuels, sauvegarde
                return;
            }
            if (card == LevelCard.Turbo) Feedback(Sfx.Chest, "TURBO grue x2 pendant " + ChestLogic.Format(LevelRules.CardTurboSeconds) + " !");
            else Feedback(Sfx.Coin, "+" + Hud.FormatMoney(LevelRules.CardMoney(Model.PlayerLevel)));
            hud.Refresh(now);
            SaveNow();
        }

        public void OpenChest(ChestKind kind)
        {
            double now = TimeUtil.NowUnix;
            if (!Model.TryOpenChest(kind, now, rng, out var reward))
            {
                Feedback(Sfx.Error, "Prochain coffre dans " + ChestLogic.Format(Model.ChestRemaining(kind, now)));
                return;
            }
            string msg = reward.money > 0 ? "Coffre : +" + Hud.FormatMoney(reward.money) : "Coffre :";
            if (reward.streakDay > 0)
                msg = "Série jour " + reward.streakDay + "/" + ChestLogic.StreakLength
                    + (reward.streakDay > 1 ? " (x" + ChestLogic.StreakFactor(reward.streakDay).ToString("0.##") + ")" : "")
                    + "\n" + msg + (reward.streakDay < ChestLogic.StreakLength ? "\nReviens demain pour la suite !" : "");
            if (reward.turboSeconds > 0) msg += "  TURBO grue x2 pendant " + ChestLogic.Format(reward.turboSeconds);
            Feedback(Sfx.Chest, msg);
            hud.Refresh(now);
            SaveNow(); // le timer du coffre doit survivre à une fermeture immédiate
        }

        public void ToggleSound() => audioManager.ToggleMute();

        /// <summary>Son + message court (l'un ou l'autre peut être nul).</summary>
        public void Feedback(Sfx? sfx, string message)
        {
            if (sfx.HasValue) audioManager.Play(sfx.Value, sfx.Value == Sfx.Horn ? 0.8f : 1f);
            if (!string.IsNullOrEmpty(message)) hud.ShowToast(message);
        }

        public void Feedback(Sfx sfx, string message) => Feedback((Sfx?)sfx, message);

        // =====================================================================
        // Sauvegarde
        // =====================================================================

        void SaveSoon()
        {
            if (saveSoonTimer < 0f) saveSoonTimer = 1.5f;
        }

        public void SaveNow()
        {
            autosaveTimer = 0f;
            saveSoonTimer = -1f;
            if (Model == null) return;

            // Un conteneur suspendu à une grue n'est dans aucune pile : on le remet
            // temporairement à sa place d'origine pour ne jamais le perdre.
            var restored = new List<KeyValuePair<StackGrid, int>>();
            foreach (var crane in cranes)
            {
                var carried = crane.Carried;
                if (carried == null) continue;
                var from = crane.CarriedFrom;
                var g = Model.GridOf(from.kind);
                if (g != null && g.Place(from.index, carried.Info)) { restored.Add(new KeyValuePair<StackGrid, int>(g, from.index)); continue; }
                var yard = Model.Data.yard;
                for (int s = 0; s < yard.SlotCount; s++)
                    if (yard.Place(s, carried.Info)) { restored.Add(new KeyValuePair<StackGrid, int>(yard, s)); break; }
            }
            SaveSystem.Save(Model.Data);
            for (int i = restored.Count - 1; i >= 0; i--) restored[i].Key.Pick(restored[i].Value);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        public void QuitGame()
        {
            SaveNow();
            Application.Quit();
        }
    }
}
