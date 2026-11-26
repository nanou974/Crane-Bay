// -----------------------------------------------------------------------------
// Crane Bay - Chef d'orchestre du jeu.
//
// Relie le modèle logique (PortModel, sans affichage) aux vues : décor,
// navire, véhicules, grue, interface, sons, sauvegarde.
// Toute la scène est construite par code au démarrage : la scène Unity ne
// contient qu'un objet avec ce composant (et même sans lui, Bootstrap le crée).
//
// Boucle de jeu :
//  1. un navire arrive et apporte une série de conteneurs ;
//  2. le joueur déplace les conteneurs vers le parc avec la grue ;
//  3. camions / trains arrivent et demandent une couleur précise ;
//  4. chaque conteneur livré rapporte de l'argent selon sa couleur ;
//  5. l'argent achète des améliorations (vitesse grue, attentes réduites).
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

        readonly System.Random rng = new System.Random();
        readonly Dictionary<int, ContainerView> containerViews = new Dictionary<int, ContainerView>();
        readonly Dictionary<Vehicle, VehicleView> vehicleViews = new Dictionary<Vehicle, VehicleView>();
        readonly List<SpriteRenderer> hintPool = new List<SpriteRenderer>();

        Camera cam;
        Transform world, yardRoot, vehiclesRoot;
        ShipView ship;
        CraneController crane;
        Hud hud;
        AudioManager audioManager;
        FloatingText floating;

        Location selected = Location.None;
        SpriteRenderer selectRing;
        ContainerView dragGhost;

        float autosaveTimer, hudTimer, saveSoonTimer = -1f;
        int lastScreenW, lastScreenH;

        public float CraneSpeedMultiplier => Model.CraneMultiplier(TimeUtil.NowUnix);
        public bool SoundMuted => audioManager.Muted;

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

            SpriteFactory.Ensure();
            SetupCamera();

            world = new GameObject("Terminal").transform;
            world.gameObject.AddComponent<WorldBuilder>().Build();
            yardRoot = new GameObject("Parc").transform;
            yardRoot.SetParent(world, false);
            vehiclesRoot = new GameObject("Vehicules").transform;
            vehiclesRoot.SetParent(world, false);

            ship = ShipView.Create(world);
            ship.Arrived += () => Feedback(null, "Un navire est à quai : déchargez-le !");

            crane = new GameObject("Grue").AddComponent<CraneController>();
            crane.transform.SetParent(world, false);
            crane.Init(this);

            floating = new GameObject("TextesFlottants").AddComponent<FloatingText>();
            floating.transform.SetParent(world, false);

            audioManager = gameObject.AddComponent<AudioManager>();
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

            // État chargé : parc et, éventuellement, navire déjà à quai.
            RebuildYardViews();
            if (Model.Data.shipDocked)
            {
                BuildShipCargoViews();
                ship.Arrive(true);
            }

            hud.Refresh(now);
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

        void Update()
        {
            double now = TimeUtil.NowUnix;
            if (Screen.width != lastScreenW || Screen.height != lastScreenH) FitCamera();

            // Navires : arrivée selon l'horloge réelle (fonctionne aussi après une fermeture).
            Model.UpdateShip(now, rng);
            if (Model.ShipNeedsDeparture && ship.IsDocked && !crane.CarriedFromShip)
            {
                Model.DepartShip(now);
                Feedback(Sfx.Horn, "Navire déchargé ! Prochain dans " + ChestLogic.Format(Model.SecondsUntilShip(now)));
                SaveSoon();
            }

            // Camions et trains.
            Model.UpdateVehicles(now, rng);
            foreach (var kv in vehicleViews)
                kv.Value.SetPatience(PortModel.PatienceRatio(kv.Key, now));

            UpdateSelectionVisuals();

            hudTimer -= Time.deltaTime;
            if (hudTimer <= 0f) { hudTimer = HudRefreshInterval; hud.Refresh(now); }

            autosaveTimer += Time.deltaTime;
            if (autosaveTimer >= AutosaveInterval) SaveNow();
            if (saveSoonTimer >= 0f)
            {
                saveSoonTimer -= Time.deltaTime;
                if (saveSoonTimer < 0f) SaveNow();
            }

            // Bouton "retour" Android : annule la sélection, sinon quitte proprement.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!selected.IsNone) ClearSelection();
                else { SaveNow(); Application.Quit(); }
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

        bool TryGetVehicleView(Location loc, out VehicleView view, out int cargoIndex)
        {
            view = null; cargoIndex = 0;
            if (!Model.TryGetDemand(loc, out var v, out _)) return false;
            if (!vehicleViews.TryGetValue(v, out view)) return false;
            cargoIndex = loc.kind == LocationKind.Wagon ? loc.index : 0;
            return true;
        }

        // =====================================================================
        // Services pour la grue
        // =====================================================================

        /// <summary>La grue peut-elle saisir à cet endroit maintenant ? (navire : seulement à quai)</summary>
        public bool CanPickNow(Location from)
        {
            if (from.kind == LocationKind.Ship && !ship.IsDocked) return false;
            return Model.CanPickFrom(from);
        }

        public bool CanDropNow(Location to, ContainerInfo c, Location from)
        {
            if (to.IsNone || c == null) return false;
            // Repli exceptionnel : retour dans la cale d'origine.
            if (to.kind == LocationKind.Ship)
                return to == from && ship.IsDocked && Model.Data.shipDocked && Model.Data.ship.CanPlace(to.index);
            if (to.IsVehicle)
            {
                if (!TryGetVehicleView(to, out var view, out _) || !view.Docked) return false;
            }
            return Model.CanDropAt(to, c, from);
        }

        public bool CanStartJob(Location from, Location to)
        {
            if (!CanPickNow(from)) return false;
            return CanDropNow(to, Model.PeekTop(from), from);
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
                if (TryGetVehicleView(to, out var view, out int idx)) return view.CargoWorld(idx);
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
            var r = Model.Drop(to, info, now);
            if (r.ok && vehicleView != null)
            {
                vehicleView.AttachContainer(idx, view);
                audioManager.Play(Sfx.Drop, 0.7f);
                audioManager.Play(Sfx.Coin);
                floating.Spawn(vehicleView.CargoWorld(idx) + new Vector3(0f, 0.6f, 0f), "+" + Hud.FormatMoney(r.earned), Palette.UiGold);
                SaveSoon();
            }
        }

        /// <summary>Destination de repli quand la destination prévue n'est plus valable.</summary>
        public Location FallbackFor(ContainerInfo c, Location from, Vector2 cranePos)
        {
            var g = Model.Data.yard;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int s = 0; s < g.SlotCount; s++)
            {
                if (!g.CanPlace(s)) continue;
                float d = ((Vector2)BoardLayout.YardSlotCenter(s) - cranePos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            if (best >= 0)
            {
                Feedback(Sfx.Error, "Destination indisponible : conteneur posé dans le parc");
                return new Location(LocationKind.Yard, best);
            }
            if (from.kind == LocationKind.Ship) return from;
            return Location.None;
        }

        // =====================================================================
        // Sélection, tap et glisser-déposer
        // =====================================================================

        /// <summary>Quel emplacement se trouve sous ce point du monde ?</summary>
        public Location HitTest(Vector2 p)
        {
            // Véhicules à quai.
            foreach (var kv in vehicleViews)
            {
                var view = kv.Value;
                if (!view.Docked) continue;
                for (int i = 0; i < view.CargoCount; i++)
                    if (BoardLayout.Inside(p, view.CargoWorld(i), view.CargoHitSize))
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
            Feedback(Sfx.Error, DestinationError(loc));
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
                if (!to.IsNone) Feedback(Sfx.Error, DestinationError(to));
                ClearSelection();
            }
        }

        bool CanQueue(Location from, Location to)
        {
            if (to.kind == LocationKind.Ship) return false;
            return CanStartJob(from, to);
        }

        string DestinationError(Location to)
        {
            if (to.kind == LocationKind.Ship) return "On ne recharge pas le navire";
            if (to.kind == LocationKind.Yard) return "Pile pleine (" + GameRules.YardMaxHeight + " conteneurs max)";
            if (to.IsVehicle) return "Ce véhicule attend une autre couleur";
            return "Destination impossible";
        }

        void Issue(Location from, Location to)
        {
            if (crane.Enqueue(from, to)) audioManager.Play(Sfx.Select);
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
            if (GameRules.IsMaxed(Model.Level(type))) { Feedback(Sfx.Error, "Niveau maximum atteint"); return; }
            if (!Model.TryBuy(type))
            {
                Feedback(Sfx.Error, "Pas assez d'argent (" + Hud.FormatMoney(Model.NextUpgradeCost(type)) + ")");
                return;
            }
            // Une attente raccourcie s'applique tout de suite au compte à rebours en cours.
            if (type == UpgradeType.AttenteNavires && !Model.Data.shipDocked)
                Model.Data.nextShipUnix = System.Math.Min(Model.Data.nextShipUnix, now + GameRules.ShipInterval(Model.Level(type)));
            if (type == UpgradeType.AttenteVehicules)
                Model.nextVehicleUnix = System.Math.Min(Model.nextVehicleUnix, now + GameRules.VehicleInterval(Model.Level(type)));

            Feedback(Sfx.Upgrade, GameRules.UpgradeNames[(int)type] + " : niveau " + Model.Level(type) + " !");
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

            // Un conteneur suspendu à la grue n'est dans aucune pile : on le remet
            // temporairement à sa place d'origine pour ne jamais le perdre.
            StackGrid restoredIn = null;
            int restoredSlot = -1;
            var carried = crane != null ? crane.Carried : null;
            if (carried != null)
            {
                var from = crane.CarriedFrom;
                var g = Model.GridOf(from.kind);
                if (g != null && g.Place(from.index, carried.Info)) { restoredIn = g; restoredSlot = from.index; }
                else if (Model.Data.yard != null)
                {
                    for (int s = 0; s < Model.Data.yard.SlotCount && restoredIn == null; s++)
                        if (Model.Data.yard.Place(s, carried.Info)) { restoredIn = Model.Data.yard; restoredSlot = s; }
                }
            }
            SaveSystem.Save(Model.Data);
            if (restoredIn != null) restoredIn.Pick(restoredSlot);
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
    }
}
