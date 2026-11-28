// -----------------------------------------------------------------------------
// Crane Bay - Modèle logique du terminal (sans aucun affichage).
//
// Toutes les règles du jeu passent par ici : arrivée/départ des navires,
// arrivée des camions et trains (annoncés à l'avance), validation des
// mouvements de grue (règles d'empilage 20/40 pieds, matières dangereuses,
// parc verrouillé), frigorifiques, paiement (bonus train et rapidité), XP et
// niveaux, contrats du jour, statistiques, améliorations, coffres.
// La couche Unity (GameManager & vues) se contente d'appeler ce modèle et
// d'animer le résultat. Sans dépendance Unity : testable hors moteur.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    /// <summary>Type d'emplacement que la grue peut viser.</summary>
    public enum LocationKind { None = 0, Ship = 1, Yard = 2, Truck = 3, Wagon = 4 }

    /// <summary>
    /// Un emplacement : une pile du navire, une pile du parc, une place de
    /// camion (index = baie) ou un wagon du train (index = wagon).
    /// </summary>
    public struct Location : IEquatable<Location>
    {
        public LocationKind kind;
        public int index;

        public Location(LocationKind kind, int index) { this.kind = kind; this.index = index; }

        public static readonly Location None = new Location(LocationKind.None, -1);
        public bool IsNone => kind == LocationKind.None;
        public bool IsVehicle => kind == LocationKind.Truck || kind == LocationKind.Wagon;
        public bool IsStack => kind == LocationKind.Ship || kind == LocationKind.Yard;

        public bool Equals(Location o) => kind == o.kind && index == o.index;
        public override bool Equals(object obj) => obj is Location l && Equals(l);
        public override int GetHashCode() => ((int)kind * 397) ^ index;
        public static bool operator ==(Location a, Location b) => a.Equals(b);
        public static bool operator !=(Location a, Location b) => !a.Equals(b);
        public override string ToString() => kind + "#" + index;
    }

    public enum VehicleKind { Camion = 0, Train = 1 }
    public enum VehicleState { Arrivee = 0, EnAttente = 1, Depart = 2 }

    /// <summary>Une demande de conteneur (une place sur un camion ou un wagon).</summary>
    public class Demand
    {
        public ContainerColor color;
        public ContainerInfo loaded; // null tant que non servi
        public bool Filled => loaded != null;
    }

    /// <summary>Camion ou train présent dans le terminal (non sauvegardé).</summary>
    public class Vehicle
    {
        public int id;
        public VehicleKind kind;
        public int bay;                 // baie de camion (0..TruckBays-1), -1 pour le train
        public VehicleState state;
        public double arrivedUnix;      // heure d'arrivée à quai (bonus de rapidité)
        public double deadlineUnix;     // au-delà, le véhicule repart (même incomplet)
        public double patienceSeconds;
        public readonly List<Demand> demands = new List<Demand>();
        public int earned;              // argent rapporté par ce véhicule

        public bool AllFilled
        {
            get { foreach (var d in demands) if (!d.Filled) return false; return true; }
        }
    }

    /// <summary>Véhicule annoncé à l'avance (liste "À venir").</summary>
    public class PlannedVehicle
    {
        public VehicleKind kind;
        public readonly List<ContainerColor> colors = new List<ContainerColor>();
    }

    /// <summary>Résultat d'une dépose par la grue.</summary>
    public struct DropResult
    {
        public bool ok;
        public int earned;          // argent gagné (0 si dépose dans le parc)
        public Vehicle vehicle;     // véhicule servi (null sinon)
        public bool vehicleDone;    // le véhicule est complet et va partir
        public bool fast;           // bonus de rapidité appliqué
        public int xp;              // XP gagnées par cette livraison
        public int levelsGained;    // nombre de niveaux franchis (0 le plus souvent)
    }

    /// <summary>Passage de niveau : nouveau niveau, prime, nouveautés débloquées.</summary>
    public struct LevelUpInfo
    {
        public int newLevel;
        public int reward;
        public int unlockedColor;   // -1 si aucune
        public string unlocks;      // texte des nouveautés (peut être vide)
        public int stars;           // bilan du niveau terminé : 1 à 3 étoiles
        public int baseReward;      // prime avant le bonus des étoiles
        public double turboSeconds; // turbo offert (3 étoiles), 0 sinon
        public int delivered, fast, missed; // bilan affiché
    }

    /// <summary>Les trois cartes proposées en fin de niveau.</summary>
    public enum LevelCard { Argent = 0, Turbo = 1, Amelioration = 2 }

    /// <summary>Pourquoi une dépose dans le parc est refusée (pour le message au joueur).</summary>
    public enum PlaceError { Aucune, Verrouille, PilePleine, QuaranteSurVingt, Dangereux }

    public class PortModel
    {
        public const double TruckPatience = 100;  // secondes avant qu'un camion reparte à vide
        public const double TrainPatience = 160;

        public SaveData Data { get; private set; }

        /// <summary>Véhicules actuellement dans le terminal.</summary>
        public readonly List<Vehicle> vehicles = new List<Vehicle>();

        /// <summary>Prochains véhicules annoncés (non sauvegardé : recalculé au lancement).</summary>
        public readonly List<PlannedVehicle> upcoming = new List<PlannedVehicle>();

        /// <summary>Prochaine arrivée de véhicule (non sauvegardé : recalculé au lancement).</summary>
        public double nextVehicleUnix;

        int plannedCounter;
        int nextVehicleId = 1;

        // Événements pour la couche d'affichage (les tests peuvent les ignorer).
        public event Action<Vehicle> VehicleSpawned;
        public event Action<Vehicle> VehicleLeaving;
        public event Action ShipArrived;
        public event Action ShipDeparting;
        public event Action MoneyChanged;
        public event Action<LevelUpInfo> LevelUp;

        /// <summary>Dernière heure connue du modèle (pour dater le turbo offert au passage de niveau).</summary>
        double clock;
        double TurboClock => clock;
        public event Action TerminalReset;
        public event Action<Contract> ContractCompleted;
        public event Action<string> RecordBroken;

        public PortModel(SaveData data, double nowUnix)
        {
            clock = nowUnix;
            Data = data ?? SaveData.NewGame(nowUnix);
            Data.Sanitize(nowUnix);
            nextVehicleUnix = nowUnix + 4; // premier camion peu après le lancement
        }

        // =====================================================================
        // Lecture d'état
        // =====================================================================

        public int Level(UpgradeType t) => Data.upgrades[(int)t];

        /// <summary>Somme des niveaux d'amélioration.</summary>
        public int UpgradeSum
        {
            get { int s = 0; foreach (var u in Data.upgrades) s += u; return s; }
        }

        /// <summary>Progression globale (améliorations + niveau) : fait grandir les coffres.</summary>
        public int Progress => UpgradeSum + (Data.level - 1);

        public int PlayerLevel => Data.level;
        public bool IsMaxLevel => Data.level >= LevelRules.MaxLevel;
        public PortTheme Theme => LevelRules.ThemeFor(Data.level);
        public bool HasSecondCrane => Level(UpgradeType.DeuxiemeGrue) >= 1;

        /// <summary>Remplissage de la barre d'XP (0..1).</summary>
        public float XpRatio
        {
            get
            {
                int need = LevelRules.XpToNext(Data.level);
                return need <= 0 ? 1f : (float)Math.Min(1.0, (double)Data.xp / need);
            }
        }

        public bool TurboActive(double now) => now < Data.turboEndUnix;

        /// <summary>Multiplicateur de vitesse de grue (amélioration x turbo x météo).</summary>
        public float CraneMultiplier(double now)
        {
            float m = GameRules.CraneSpeedMultiplier(Level(UpgradeType.VitesseGrue));
            if (TurboActive(now)) m *= GameRules.TurboMultiplier;
            return m * Ambience.CraneFactor(Ambience.WeatherAt(now));
        }

        public StackGrid GridOf(LocationKind kind) =>
            kind == LocationKind.Ship ? Data.ship : kind == LocationKind.Yard ? Data.yard : null;

        public Vehicle TruckAt(int bay)
        {
            foreach (var v in vehicles) if (v.kind == VehicleKind.Camion && v.bay == bay) return v;
            return null;
        }

        public Vehicle Train
        {
            get { foreach (var v in vehicles) if (v.kind == VehicleKind.Train) return v; return null; }
        }

        /// <summary>Véhicule + demande correspondant à un emplacement véhicule.</summary>
        public bool TryGetDemand(Location loc, out Vehicle vehicle, out Demand demand)
        {
            vehicle = null; demand = null;
            if (loc.kind == LocationKind.Truck) { vehicle = TruckAt(loc.index); if (vehicle != null) demand = vehicle.demands[0]; }
            else if (loc.kind == LocationKind.Wagon)
            {
                vehicle = Train;
                if (vehicle != null && loc.index >= 0 && loc.index < vehicle.demands.Count) demand = vehicle.demands[loc.index];
            }
            return vehicle != null && demand != null;
        }

        // =====================================================================
        // Parc : zones débloquées et règles d'empilage
        // =====================================================================

        public int YardRowsUnlocked => GameRules.YardRowsUnlocked(Level(UpgradeType.AgrandirParc));
        public int YardHeightLimit => GameRules.YardHeightLimit(Level(UpgradeType.AgrandirParc));

        public bool IsYardSlotUnlocked(int slot) =>
            Data.yard.IsValidSlot(slot) && Data.yard.RowOf(slot) < YardRowsUnlocked;

        public static bool IsPoweredSlot(int slot) => slot / GameRules.YardColumns == GameRules.PoweredRow;

        /// <summary>Capacité utilisable du parc (zones débloquées et hauteur autorisée).</summary>
        public int YardUsableCapacity => GameRules.YardColumns * YardRowsUnlocked * YardHeightLimit;

        public int YardFreeCapacity
        {
            get
            {
                int free = 0;
                for (int s = 0; s < Data.yard.SlotCount; s++)
                    if (IsYardSlotUnlocked(s)) free += Math.Max(0, YardHeightLimit - Data.yard.Height(s));
                return free;
            }
        }

        /// <summary>Vérifie toutes les règles d'empilage du parc pour ce conteneur.</summary>
        public PlaceError CheckYardPlace(int slot, ContainerInfo c)
        {
            if (!IsYardSlotUnlocked(slot)) return PlaceError.Verrouille;
            if (Data.yard.Height(slot) >= YardHeightLimit || !Data.yard.CanPlace(slot)) return PlaceError.PilePleine;
            var top = Data.yard.Top(slot);
            // Règle réelle : un 40 pieds repose sur les 4 coins d'un autre 40 pieds, jamais sur un 20 pieds.
            if (!c.IsSmall && top != null && top.IsSmall) return PlaceError.QuaranteSurVingt;
            if (c.hazard && HazardNear(slot)) return PlaceError.Dangereux;
            return PlaceError.Aucune;
        }

        /// <summary>Un conteneur dangereux est-il dans cette pile ou une pile voisine (haut/bas/gauche/droite) ?</summary>
        bool HazardNear(int slot)
        {
            var g = Data.yard;
            int c = g.ColumnOf(slot), r = g.RowOf(slot);
            int[] dc = { 0, 1, -1, 0, 0 };
            int[] dr = { 0, 0, 0, 1, -1 };
            for (int i = 0; i < dc.Length; i++)
            {
                int nc = c + dc[i], nr = r + dr[i];
                if (nc < 0 || nr < 0 || nc >= g.columns || nr >= g.rows) continue;
                foreach (var item in g.stacks[g.SlotIndex(nc, nr)].items)
                    if (item.hazard) return true;
            }
            return false;
        }

        // =====================================================================
        // Règles de la grue
        // =====================================================================

        /// <summary>Peut-on saisir un conteneur ici ? (dessus d'une pile seulement)</summary>
        public bool CanPickFrom(Location from)
        {
            if (from.kind == LocationKind.Ship) return Data.shipDocked && Data.ship.CanPick(from.index);
            if (from.kind == LocationKind.Yard) return Data.yard.CanPick(from.index);
            return false; // on ne reprend pas un conteneur chargé sur un véhicule
        }

        public ContainerInfo PeekTop(Location from)
        {
            var g = GridOf(from.kind);
            return g?.Top(from.index);
        }

        /// <summary>Peut-on déposer ce conteneur à cet endroit ?</summary>
        public bool CanDropAt(Location to, ContainerInfo c, Location from)
        {
            if (c == null || to == from) return false;
            switch (to.kind)
            {
                case LocationKind.Yard:
                    return CheckYardPlace(to.index, c) == PlaceError.Aucune;
                case LocationKind.Truck:
                case LocationKind.Wagon:
                    if (!TryGetDemand(to, out var v, out var d)) return false;
                    return v.state == VehicleState.EnAttente && !d.Filled && d.color == c.Color;
                default:
                    return false; // on ne recharge pas le navire
            }
        }

        /// <summary>Validation complète d'un mouvement source -> destination.</summary>
        public bool CanMove(Location from, Location to) =>
            CanPickFrom(from) && CanDropAt(to, PeekTop(from), from);

        /// <summary>Retire le conteneur du dessus (la grue l'a saisi).</summary>
        public ContainerInfo Pick(Location from)
        {
            if (!CanPickFrom(from)) return null;
            return GridOf(from.kind).Pick(from.index);
        }

        /// <summary>
        /// Repli de sécurité : remet un conteneur dans la cale d'où il vient
        /// (utilisé si plus aucune place n'est disponible ailleurs).
        /// </summary>
        public bool ReturnToShip(int slot, ContainerInfo c) =>
            Data.shipDocked && c != null && Data.ship.Place(slot, c);

        /// <summary>Le navire a-t-il été entièrement déchargé ?</summary>
        public bool ShipNeedsDeparture => Data.shipDocked && Data.ship.IsEmpty;

        /// <summary>Pose le conteneur porté par la grue. Paie si c'est une livraison.</summary>
        public DropResult Drop(Location to, ContainerInfo c, double now)
        {
            clock = Math.Max(clock, now);
            var res = new DropResult();
            if (c == null) return res;
            if (to.kind == LocationKind.Yard)
            {
                if (CheckYardPlace(to.index, c) != PlaceError.Aucune) return res;
                res.ok = Data.yard.Place(to.index, c);
                return res;
            }
            if (!to.IsVehicle || !TryGetDemand(to, out var v, out var d)) return res;
            if (v.state != VehicleState.EnAttente || d.Filled || d.color != c.Color) return res;

            bool train = v.kind == VehicleKind.Train;
            res.fast = now - v.arrivedUnix <= GameRules.FastDeliverySeconds;
            d.loaded = c;
            int gain = (int)Math.Round(GameRules.Payout(c, train, res.fast) * GameRules.IncomeMultiplier(Level(UpgradeType.Revenus)));
            AddMoney(gain);
            Data.delivered++;
            if (res.fast) Data.fastDeliveries++;
            Data.levelDelivered++;
            if (res.fast) Data.levelFast++;
            if (gain > Data.bestSingleDelivery) { Data.bestSingleDelivery = gain; }
            v.earned += gain;
            res.ok = true;
            res.earned = gain;
            res.vehicle = v;
            res.xp = LevelRules.XpFor(c.Color, train);

            if (v.AllFilled)
            {
                res.vehicleDone = true;
                if (train)
                {
                    Data.trainsCompleted++;
                    if (v.earned > Data.bestTrainEarned)
                    {
                        bool had = Data.bestTrainEarned > 0;
                        Data.bestTrainEarned = v.earned;
                        if (had) RecordBroken?.Invoke("Record : plus gros train (" + v.earned + " $)");
                    }
                }
                StartLeaving(v, now);
            }

            // XP de la livraison puis des contrats éventuels (peuvent faire monter de niveau).
            int levels = AddXp(res.xp);
            levels += ProgressContracts(c, train, res.fast);
            res.levelsGained = levels;

            // Nouveau niveau : le terminal repart de zéro.
            if (res.levelsGained > 0) ResetTerminal(now);
            return res;
        }

        // =====================================================================
        // Frigorifiques
        // =====================================================================

        /// <summary>
        /// Fait vieillir les frigorifiques débranchés (dans le parc, hors rangée
        /// électrifiée). Le navire et les véhicules ont leurs propres prises.
        /// Renvoie true si au moins un conteneur a perdu de la valeur.
        /// </summary>
        public bool UpdateReefers(float dt)
        {
            bool changed = false;
            var g = Data.yard;
            for (int s = 0; s < g.SlotCount; s++)
            {
                if (IsPoweredSlot(s)) continue;
                foreach (var c in g.stacks[s].items)
                {
                    if (!c.reefer || c.unplugged >= GameRules.ReeferSpoilSeconds) continue;
                    c.unplugged = Math.Min(GameRules.ReeferSpoilSeconds, c.unplugged + dt);
                    changed = true;
                }
            }
            return changed;
        }

        // =====================================================================
        // Navires
        // =====================================================================

        /// <summary>Met à jour l'arrivée du navire. Renvoie true si un navire vient d'arriver.</summary>
        public bool UpdateShip(double now, Random rng)
        {
            clock = Math.Max(clock, now);
            if (Data.shipDocked || now < Data.nextShipUnix) return false;
            FillShip(rng);
            Data.shipDocked = true;
            Data.shipDockedSinceUnix = now;
            ShipArrived?.Invoke();
            return true;
        }

        /// <summary>Charge le navire d'une série de conteneurs aléatoires, empilés proprement.</summary>
        void FillShip(Random rng)
        {
            var ship = new StackGrid(GameRules.ShipColumns, GameRules.ShipRows, GameRules.ShipMaxHeight);
            int cargo = rng.Next(GameRules.ShipMinCargo, GameRules.ShipMaxCargo + 1);
            // Plus le joueur monte de niveau, plus les navires arrivent chargés.
            cargo = Math.Min(ship.Capacity, cargo + LevelRules.ExtraCargo(Data.level));
            var free = new List<int>();
            for (int i = 0; i < cargo; i++)
            {
                var c = LevelRules.RollContainer(rng, Data.level, Data.nextContainerId);
                free.Clear();
                for (int s = 0; s < ship.SlotCount; s++)
                {
                    if (!ship.CanPlace(s)) continue;
                    var top = ship.Top(s);
                    if (!c.IsSmall && top != null && top.IsSmall) continue; // même règle qu'au parc
                    free.Add(s);
                }
                if (free.Count == 0) { c.size = 20; for (int s = 0; s < ship.SlotCount; s++) if (ship.CanPlace(s)) free.Add(s); }
                if (free.Count == 0) break;
                Data.nextContainerId++;
                ship.Place(free[rng.Next(free.Count)], c);
            }
            Data.ship = ship;
        }

        /// <summary>Le navire vide repart ; le prochain arrivera après l'attente (améliorable).</summary>
        public void DepartShip(double now)
        {
            if (!Data.shipDocked) return;
            Data.shipDocked = false;
            Data.nextShipUnix = now + GameRules.ShipInterval(Level(UpgradeType.AttenteNavires));
            Data.shipsUnloaded++;
            if (Data.shipDockedSinceUnix > 0)
            {
                double t = now - Data.shipDockedSinceUnix;
                if (t > 0 && (Data.bestUnloadSeconds <= 0 || t < Data.bestUnloadSeconds))
                {
                    bool had = Data.bestUnloadSeconds > 0;
                    Data.bestUnloadSeconds = t;
                    if (had) RecordBroken?.Invoke("Record : navire déchargé en " + ChestLogic.Format(t));
                }
            }
            ShipDeparting?.Invoke();
        }

        public double SecondsUntilShip(double now) => Data.shipDocked ? 0 : Math.Max(0, Data.nextShipUnix - now);

        // =====================================================================
        // Camions et trains (annoncés à l'avance)
        // =====================================================================

        /// <summary>
        /// Gère les arrivées et les départs par impatience. Renvoie le véhicule
        /// créé (ou null). Les véhicules arrivent en état "Arrivee" : la vue
        /// appelle MarkArrived() quand l'animation d'approche est terminée.
        /// </summary>
        public Vehicle UpdateVehicles(double now, Random rng)
        {
            clock = Math.Max(clock, now);
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.state == VehicleState.EnAttente && now >= v.deadlineUnix) StartLeaving(v, now);
            }

            // Niveau en attente : il est validé dès que les véhicules présents sont partis
            // ou servis. En attendant, aucun nouveau véhicule n'arrive.
            if (LevelUpPending)
            {
                TryLevelUp(now);
                return null;
            }

            RefillUpcoming(rng);
            if (now < nextVehicleUnix) return null;

            Vehicle created = TrySpawnVehicle(rng);
            // Si rien n'a pu arriver (places occupées ou aucun conteneur à demander),
            // on réessaie bientôt sans attendre tout l'intervalle.
            nextVehicleUnix = created != null
                ? now + GameRules.VehicleInterval(Level(UpgradeType.AttenteVehicules))
                : now + 2;
            return created;
        }

        /// <summary>Complète la liste des véhicules annoncés.</summary>
        void RefillUpcoming(Random rng)
        {
            if (upcoming.Count >= GameRules.UpcomingCount) return;
            var available = AvailableColorCounts();
            foreach (var p in upcoming) foreach (var col in p.colors) available[(int)col]--;
            while (upcoming.Count < GameRules.UpcomingCount)
            {
                // Un véhicule sur trois est un train.
                plannedCounter++;
                var p = new PlannedVehicle
                {
                    kind = plannedCounter % GameRules.TrainEvery == 0 ? VehicleKind.Train : VehicleKind.Camion,
                };
                int count = p.kind == VehicleKind.Train ? GameRules.TrainWagons : 1;
                for (int i = 0; i < count; i++)
                {
                    var color = PickDemandColor(available, rng, Data.level);
                    available[(int)color]--;
                    p.colors.Add(color);
                }
                upcoming.Add(p);
            }
        }

        Vehicle TrySpawnVehicle(Random rng)
        {
            if (upcoming.Count == 0) return null;
            var available = AvailableColorCounts();
            int totalAvailable = 0;
            foreach (var n in available) totalAvailable += n;
            if (totalAvailable == 0) return null;

            int freeBay = -1;
            for (int b = 0; b < GameRules.TruckBays; b++) if (TruckAt(b) == null) { freeBay = b; break; }
            bool railFree = Train == null;

            // On prend le premier véhicule annoncé qui a une place libre
            // (un train ne bloque pas les camions si la voie est occupée, et inversement).
            int pick = -1;
            bool train = false;
            for (int i = 0; i < upcoming.Count && pick < 0; i++)
            {
                bool wantsTrain = upcoming[i].kind == VehicleKind.Train;
                // Pas assez de conteneurs pour un train : il arrive en camion.
                if (wantsTrain && totalAvailable < GameRules.TrainWagons) wantsTrain = false;
                if (wantsTrain && railFree) { pick = i; train = true; }
                else if (!wantsTrain && freeBay >= 0) { pick = i; train = false; }
            }
            if (pick < 0) return null;
            var plan = upcoming[pick];

            var v = new Vehicle
            {
                id = nextVehicleId++,
                kind = train ? VehicleKind.Train : VehicleKind.Camion,
                bay = train ? -1 : freeBay,
                state = VehicleState.Arrivee,
                patienceSeconds = train ? TrainPatience : TruckPatience,
            };
            int count = train ? GameRules.TrainWagons : 1;
            for (int i = 0; i < count; i++)
            {
                // On respecte l'annonce si possible ; sinon on remplace par une couleur disponible.
                ContainerColor color = i < plan.colors.Count ? plan.colors[i] : ContainerColor.Blanc;
                if (available[(int)color] <= 0) color = PickDemandColor(available, rng, Data.level);
                available[(int)color]--;
                v.demands.Add(new Demand { color = color });
            }
            upcoming.RemoveAt(pick);
            vehicles.Add(v);
            VehicleSpawned?.Invoke(v);
            return v;
        }

        /// <summary>
        /// Conteneurs disponibles par couleur (parc + navire), moins ceux déjà
        /// demandés par des véhicules en attente. Quand le parc est presque plein,
        /// on ne demande que des conteneurs accessibles (au sommet d'une pile)
        /// pour éviter un blocage impossible à résoudre.
        /// </summary>
        public int[] AvailableColorCounts()
        {
            var counts = new int[GameRules.ColorCount];
            if (YardFreeCapacity <= 1)
            {
                AddTops(Data.yard, counts);
                if (Data.shipDocked) AddTops(Data.ship, counts);
            }
            else
            {
                Data.yard.AddColorCounts(counts);
                if (Data.shipDocked) Data.ship.AddColorCounts(counts);
            }
            foreach (var v in vehicles)
            {
                if (v.state == VehicleState.Depart) continue;
                foreach (var d in v.demands)
                    if (!d.Filled) counts[(int)d.color]--;
            }
            for (int i = 0; i < counts.Length; i++) if (counts[i] < 0) counts[i] = 0;
            return counts;
        }

        static void AddTops(StackGrid g, int[] counts)
        {
            for (int s = 0; s < g.SlotCount; s++)
            {
                var t = g.Top(s);
                if (t != null) counts[t.color]++;
            }
        }

        /// <summary>
        /// Tire une couleur proportionnellement à la disponibilité ; s'il n'y a
        /// rien de disponible (annonce lointaine), on tire parmi les couleurs débloquées.
        /// </summary>
        static ContainerColor PickDemandColor(int[] available, Random rng, int level)
        {
            int total = 0;
            foreach (var n in available) if (n > 0) total += n;
            if (total <= 0) return LevelRules.RollColor(rng, level);
            int r = rng.Next(total);
            for (int i = 0; i < available.Length; i++)
            {
                if (available[i] <= 0) continue;
                if (r < available[i]) return (ContainerColor)i;
                r -= available[i];
            }
            return ContainerColor.Blanc;
        }

        /// <summary>Le véhicule est arrivé à sa place : il commence à attendre.</summary>
        public void MarkArrived(Vehicle v, double now)
        {
            if (v.state != VehicleState.Arrivee) return;
            v.state = VehicleState.EnAttente;
            v.arrivedUnix = now;
            v.deadlineUnix = now + v.patienceSeconds;
        }

        void StartLeaving(Vehicle v, double now)
        {
            if (v.state == VehicleState.Depart) return;
            if (!v.AllFilled) Data.levelMissed++;
            v.state = VehicleState.Depart;
            VehicleLeaving?.Invoke(v);
        }

        // =====================================================================
        // Carte au choix de fin de niveau
        // =====================================================================

        public bool HasPendingCard => Data.pendingCards > 0;

        /// <summary>
        /// Amélioration offerte par la carte "Amélioration" : la moins chère des
        /// améliorations pas encore au maximum. -1 si tout est au maximum.
        /// </summary>
        public int FreeUpgradeOption()
        {
            int best = -1;
            long bestCost = long.MaxValue;
            for (int i = 0; i < GameRules.UpgradeCount; i++)
            {
                var t = (UpgradeType)i;
                if (GameRules.IsMaxed(t, Level(t))) continue;
                long cost = NextUpgradeCost(t);
                if (cost < bestCost) { bestCost = cost; best = i; }
            }
            return best;
        }

        /// <summary>Applique la carte choisie. Renvoie false s'il n'y a pas de carte à choisir.</summary>
        public bool ChooseCard(LevelCard card, double now)
        {
            if (!HasPendingCard) return false;
            int upgrade = FreeUpgradeOption();
            // Tout est déjà au maximum : la carte amélioration devient de l'argent.
            if (card == LevelCard.Amelioration && upgrade < 0) card = LevelCard.Argent;
            switch (card)
            {
                case LevelCard.Argent:
                    AddMoney(LevelRules.CardMoney(Data.level));
                    break;
                case LevelCard.Turbo:
                    Data.turboEndUnix = Math.Max(Data.turboEndUnix, now) + LevelRules.CardTurboSeconds;
                    break;
                default:
                    Data.upgrades[upgrade]++;
                    break;
            }
            Data.pendingCards--;
            return true;
        }

        /// <summary>Jour de série (1 à 7) qu'apportera la prochaine ouverture du coffre du jour.</summary>
        public int NextStreakDay(double now) =>
            ChestLogic.StreakDay(ChestLogic.NextStreak(Data.dailyStreak, Data.lastDailyChestUnix, now));

        /// <summary>
        /// Pause : décale toutes les échéances de jeu (navire, véhicules, turbo,
        /// record de déchargement) de la durée de la pause, pour que rien n'avance.
        /// Les coffres, eux, suivent l'heure réelle et ne sont pas décalés.
        /// </summary>
        public void ShiftTimers(double seconds)
        {
            if (seconds <= 0) return;
            if (!Data.shipDocked) Data.nextShipUnix += seconds;
            if (Data.shipDockedSinceUnix > 0) Data.shipDockedSinceUnix += seconds;
            if (Data.turboEndUnix > 0) Data.turboEndUnix += seconds;
            nextVehicleUnix += seconds;
            foreach (var v in vehicles)
            {
                v.arrivedUnix += seconds;
                v.deadlineUnix += seconds;
            }
        }

        /// <summary>Le véhicule a quitté l'écran : on libère sa place.</summary>
        public void RemoveVehicle(Vehicle v) => vehicles.Remove(v);

        /// <summary>Patience restante (0..1) pour l'affichage.</summary>
        public static float PatienceRatio(Vehicle v, double now)
        {
            if (v.state != VehicleState.EnAttente) return 1f;
            return (float)Math.Max(0, Math.Min(1, (v.deadlineUnix - now) / v.patienceSeconds));
        }

        /// <summary>Le bonus de rapidité est-il encore possible pour ce véhicule ?</summary>
        public static bool FastBonusActive(Vehicle v, double now) =>
            v.state == VehicleState.EnAttente && now - v.arrivedUnix <= GameRules.FastDeliverySeconds;

        // =====================================================================
        // Niveaux
        // =====================================================================

        /// <summary>
        /// Remise à zéro du terminal (au passage de niveau) : parc et navire vidés,
        /// tous les véhicules repartent, un nouveau navire arrive peu après.
        /// L'argent, les améliorations, le niveau, les contrats et les coffres sont conservés.
        /// </summary>
        public void ResetTerminal(double now)
        {
            Data.yard = new StackGrid(GameRules.YardColumns, GameRules.YardRows, GameRules.YardMaxHeight);
            Data.ship = new StackGrid(GameRules.ShipColumns, GameRules.ShipRows, GameRules.ShipMaxHeight);
            Data.shipDocked = false;
            Data.shipDockedSinceUnix = 0;
            Data.nextShipUnix = now + 4;
            // Départ silencieux : ce n'est pas une impatience du chauffeur.
            foreach (var v in vehicles) v.state = VehicleState.Depart;
            upcoming.Clear();
            nextVehicleUnix = now + 8;
            TerminalReset?.Invoke();
        }

        /// <summary>
        /// La barre d'XP est pleine mais le niveau n'est pas encore validé :
        /// il faut d'abord servir (ou laisser partir) les camions et trains présents.
        /// </summary>
        public bool LevelUpPending => !IsMaxLevel && Data.xp >= LevelRules.XpToNext(Data.level);

        /// <summary>Un camion ou un train présent attend encore des conteneurs.</summary>
        public bool VehiclesWaiting
        {
            get
            {
                foreach (var v in vehicles) if (v.state != VehicleState.Depart && !v.AllFilled) return true;
                return false;
            }
        }

        /// <summary>
        /// Valide un passage de niveau en attente dès que plus aucun véhicule n'attend.
        /// Remet alors le terminal à zéro. Renvoie le nombre de niveaux gagnés.
        /// </summary>
        public int TryLevelUp(double now)
        {
            clock = Math.Max(clock, now);
            int gained = ApplyLevels();
            if (gained > 0) ResetTerminal(now);
            return gained;
        }

        /// <summary>
        /// Ajoute de l'XP. Le passage de niveau n'a lieu que si aucun camion ou
        /// train présent n'attend de conteneur ; sinon il reste en attente
        /// (voir LevelUpPending / TryLevelUp). Renvoie le nombre de niveaux gagnés.
        /// </summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;
            Data.xp += amount;
            return ApplyLevels();
        }

        int ApplyLevels()
        {
            if (!LevelUpPending || VehiclesWaiting) return 0;
            int gained = 0;
            while (!IsMaxLevel && Data.xp >= LevelRules.XpToNext(Data.level))
            {
                Data.xp -= LevelRules.XpToNext(Data.level);
                Data.level++;
                gained++;
                // Bilan en étoiles du niveau qui se termine (le premier, si plusieurs d'un coup).
                int stars = gained == 1 ? LevelRules.Stars(Data.levelDelivered, Data.levelFast, Data.levelMissed) : 1;
                int baseReward = LevelRules.LevelUpReward(Data.level);
                var info = new LevelUpInfo
                {
                    newLevel = Data.level,
                    baseReward = baseReward,
                    stars = stars,
                    reward = (int)Math.Round(baseReward * LevelRules.StarFactor(stars)),
                    turboSeconds = stars >= 3 ? LevelRules.ThreeStarTurboSeconds : 0,
                    delivered = Data.levelDelivered,
                    fast = Data.levelFast,
                    missed = Data.levelMissed,
                    unlockedColor = LevelRules.ColorUnlockedAt(Data.level),
                    unlocks = LevelRules.UnlocksAt(Data.level),
                };
                Data.levelDelivered = Data.levelFast = Data.levelMissed = 0;
                Data.pendingCards = Math.Min(5, Data.pendingCards + 1);
                AddMoney(info.reward);
                if (info.turboSeconds > 0)
                    Data.turboEndUnix = Math.Max(Data.turboEndUnix, TurboClock) + info.turboSeconds;
                LevelUp?.Invoke(info);
            }
            if (IsMaxLevel) Data.xp = 0;
            return gained;
        }

        // =====================================================================
        // Contrats du jour
        // =====================================================================

        /// <summary>Renouvelle les contrats au changement de jour (UTC). Renvoie true si renouvelés.</summary>
        public bool EnsureContracts(double now, Random rng)
        {
            long day = ContractRules.DayIndex(now);
            if (Data.contractsDay == day && Data.contracts.Count == ContractRules.Count) return false;
            Data.contractsDay = day;
            Data.contracts = ContractRules.Generate(rng, Data.level);
            return true;
        }

        /// <summary>Fait progresser les contrats. Renvoie le nombre de niveaux gagnés grâce aux récompenses.</summary>
        int ProgressContracts(ContainerInfo c, bool train, bool fast)
        {
            int levels = 0;
            foreach (var k in Data.contracts)
            {
                if (!ContractRules.Matches(k, c, train, fast)) continue;
                k.progress++;
                if (k.progress < k.target) continue;
                k.done = true;
                AddMoney((long)Math.Round(k.rewardMoney * GameRules.IncomeMultiplier(Level(UpgradeType.Revenus))));
                ContractCompleted?.Invoke(k);
                levels += AddXp(k.rewardXp);
            }
            return levels;
        }

        // =====================================================================
        // Économie
        // =====================================================================

        void AddMoney(long amount)
        {
            Data.money += amount;
            if (amount > 0) Data.totalEarned += amount;
            MoneyChanged?.Invoke();
        }

        public int NextUpgradeCost(UpgradeType t) => GameRules.UpgradeCost(t, Level(t), Data.level);

        public bool CanBuy(UpgradeType t) =>
            !GameRules.IsMaxed(t, Level(t)) && Data.money >= NextUpgradeCost(t);

        /// <summary>Achète le niveau suivant d'une amélioration si possible.</summary>
        public bool TryBuy(UpgradeType t)
        {
            if (!CanBuy(t)) return false;
            Data.money -= NextUpgradeCost(t);
            Data.upgrades[(int)t]++;
            MoneyChanged?.Invoke();
            return true;
        }

        // =====================================================================
        // Coffres
        // =====================================================================

        public double ChestRemaining(ChestKind kind, double now) =>
            ChestLogic.Remaining(kind, kind == ChestKind.Quotidien ? Data.lastDailyChestUnix : Data.lastQuickChestUnix, now);

        /// <summary>Ouvre un coffre s'il est prêt et applique sa récompense.</summary>
        public bool TryOpenChest(ChestKind kind, double now, Random rng, out ChestReward reward)
        {
            reward = default;
            if (ChestRemaining(kind, now) > 0) return false;
            reward = ChestLogic.Roll(kind, Progress, rng);
            if (kind == ChestKind.Quotidien)
            {
                // Série de jours : à calculer AVANT de mettre à jour l'heure d'ouverture.
                Data.dailyStreak = ChestLogic.NextStreak(Data.dailyStreak, Data.lastDailyChestUnix, now);
                reward = ChestLogic.ApplyStreak(reward, ChestLogic.StreakDay(Data.dailyStreak));
            }
            if (kind == ChestKind.Quotidien) Data.lastDailyChestUnix = now;
            else Data.lastQuickChestUnix = now;
            if (reward.money > 0) AddMoney(reward.money);
            if (reward.turboSeconds > 0)
                Data.turboEndUnix = Math.Max(Data.turboEndUnix, now) + reward.turboSeconds;
            return true;
        }
    }
}
