// Tests de la logique de Crane Bay, sans Unity.
// Lancer : dotnet run --project Tests/CoreTests
using System;
using System.Collections.Generic;
using CraneBay.Core;

static class Program
{
    static int failures, checks;

    static void Check(bool cond, string what)
    {
        checks++;
        if (!cond) { failures++; Console.WriteLine("ECHEC : " + what); }
    }

    static int Main()
    {
        StackRules();
        ChestTimers();
        UpgradeCosts();
        SanitizeSave();
        FullGameLoop();
        VehiclePatienceAndNoDeadlock();
        PlayerLevels();
        LevelUpResetsTerminal();
        NewStackingRules();
        ReefersAndBonus();
        UpcomingAndContracts();
        AmbienceThemesRecords();
        LevelUpWaitsForVehicles();
        Music();
        PauseAndIncome();
        Console.WriteLine(failures == 0 ? $"OK : {checks} vérifications réussies" : $"{failures}/{checks} vérifications en échec");
        return failures == 0 ? 0 : 1;
    }

    static void StackRules()
    {
        var g = new StackGrid(2, 1, 3);
        Check(!g.CanPick(0), "pile vide : rien à saisir");
        Check(g.Place(0, new ContainerInfo(1, ContainerColor.Bleu)), "pose 1");
        Check(g.Place(0, new ContainerInfo(2, ContainerColor.Rouge)), "pose 2");
        Check(g.Place(0, new ContainerInfo(3, ContainerColor.Vert)), "pose 3");
        Check(!g.Place(0, new ContainerInfo(4, ContainerColor.Vert)), "hauteur max respectée");
        Check(g.Top(0).id == 3, "le dessus est le dernier posé");
        Check(g.BlockersAbove(0, ContainerColor.Bleu) == 2, "2 conteneurs bloquent le bleu");
        Check(g.Pick(0).id == 3 && g.Pick(0).id == 2, "on ne prend que par le dessus");
        Check(g.TotalCount() == 1, "comptage");
        Check(!g.IsValidSlot(5) && g.Pick(5) == null, "emplacement invalide");
    }

    static void ChestTimers()
    {
        double t0 = 1_700_000_000;
        Check(ChestLogic.IsReady(ChestKind.VingtMinutes, 0, t0), "jamais ouvert = prêt");
        Check(!ChestLogic.IsReady(ChestKind.VingtMinutes, t0, t0 + 60), "pas prêt après 1 min");
        Check(Math.Abs(ChestLogic.Remaining(ChestKind.VingtMinutes, t0, t0 + 60) - 1140) < 1e-6, "19 min restantes");
        // Jeu fermé pendant 3 heures : le coffre s'est régénéré hors ligne.
        Check(ChestLogic.IsReady(ChestKind.VingtMinutes, t0, t0 + 3 * 3600), "régénéré hors ligne");
        Check(!ChestLogic.IsReady(ChestKind.Quotidien, t0, t0 + 23 * 3600), "quotidien : pas avant 24 h");
        Check(ChestLogic.IsReady(ChestKind.Quotidien, t0, t0 + 24 * 3600), "quotidien : prêt après 24 h");
        // Horloge reculée d'un an : on n'attend jamais plus que la durée normale.
        Check(ChestLogic.Remaining(ChestKind.VingtMinutes, t0, t0 - 365 * 86400) <= 20 * 60, "horloge reculée bornée");
        Check(ChestLogic.Format(0) == "Prêt !" && ChestLogic.Format(65) == "1:05" && ChestLogic.Format(3 * 3600 + 120) == "3 h 02", "formatage");

        var rng = new Random(1);
        for (int i = 0; i < 200; i++)
        {
            var r = ChestLogic.Roll(ChestKind.VingtMinutes, 3, rng);
            Check(r.money > 0 || r.turboSeconds > 0, "un coffre contient toujours quelque chose");
        }
    }

    static void UpgradeCosts()
    {
        foreach (UpgradeType t in Enum.GetValues(typeof(UpgradeType)))
            for (int l = 0; l < GameRules.MaxLevelOf(t) - 1; l++)
                Check(GameRules.UpgradeCost(t, l + 1) > GameRules.UpgradeCost(t, l), $"coût croissant {t} {l}");
        foreach (UpgradeType t in Enum.GetValues(typeof(UpgradeType)))
            for (int pl = 1; pl < LevelRules.MaxLevel; pl++)
                Check(GameRules.UpgradeCost(t, 0, pl + 1) > GameRules.UpgradeCost(t, 0, pl), $"prix croissant avec le niveau joueur {t} {pl}");
        var mm = new PortModel(null, 1_700_000_000);
        int c1 = mm.NextUpgradeCost(UpgradeType.VitesseGrue);
        mm.AddXp(LevelRules.XpToNext(1));
        Check(mm.NextUpgradeCost(UpgradeType.VitesseGrue) > c1, "le passage de niveau renchérit les améliorations");
        Check(GameRules.ShipInterval(5) < GameRules.ShipInterval(0), "attente navires réduite");
        Check(GameRules.VehicleInterval(5) < GameRules.VehicleInterval(0), "attente véhicules réduite");
        Check(GameRules.CraneSpeedMultiplier(5) > 1.9f, "grue plus rapide");
        Check(GameRules.Payout(ContainerColor.Dore, false) == 250 && GameRules.Payout(ContainerColor.Blanc, true) == 15, "paiements");
    }

    static void SanitizeSave()
    {
        double now = 1_700_000_000;
        var d = new SaveData { upgrades = new[] { 99 }, money = -5, yard = new StackGrid(2, 2, 2), ship = null, shipDocked = true, nextShipUnix = now + 1e9 };
        d.yard.Place(0, new ContainerInfo(42, ContainerColor.Rouge));
        d.Sanitize(now);
        Check(d.upgrades.Length == GameRules.UpgradeCount && d.upgrades[0] == GameRules.UpgradeMaxLevel, "niveaux réparés");
        Check(d.money == 0, "argent négatif corrigé");
        Check(d.yard.HasShape(GameRules.YardColumns, GameRules.YardRows, GameRules.YardMaxHeight) && d.yard.TotalCount() == 1, "parc redimensionné sans perte");
        Check(!d.shipDocked, "navire vide renvoyé");
        Check(d.nextContainerId > 42, "identifiants uniques");
        Check(d.nextShipUnix <= now + GameRules.ShipInterval(0), "attente navire bornée");
    }

    /// <summary>
    /// Critère de réussite : une partie complète, de l'arrivée du navire à
    /// l'achat d'une amélioration, en jouant comme un joueur (grue = Pick/Drop).
    /// </summary>
    static void FullGameLoop()
    {
        var rng = new Random(7);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        int ships = 0, deliveries = 0;
        bool bought = false;

        for (int step = 0; step < 20000 && !bought; step++)
        {
            now += 0.5;
            if (m.UpdateShip(now, rng)) ships++;
            var v = m.UpdateVehicles(now, rng);
            if (v != null) m.MarkArrived(v, now); // l'animation d'arrivée est instantanée ici
            foreach (var veh in m.vehicles.ToArray())
                if (veh.state == VehicleState.Depart) m.RemoveVehicle(veh);

            // 1) Servir un véhicule si un conteneur demandé est accessible.
            bool acted = TryServe(m, now);
            // 2) Sinon décharger le navire vers le parc.
            if (!acted && m.Data.shipDocked)
            {
                for (int s = 0; s < m.Data.ship.SlotCount && !acted; s++)
                {
                    var from = new Location(LocationKind.Ship, s);
                    if (!m.CanPickFrom(from)) continue;
                    for (int y = 0; y < m.Data.yard.SlotCount && !acted; y++)
                    {
                        var to = new Location(LocationKind.Yard, y);
                        if (m.CanMove(from, to)) { Move(m, from, to, now); acted = true; }
                    }
                }
            }
            if (m.ShipNeedsDeparture) m.DepartShip(now);
            if (m.CanBuy(UpgradeType.VitesseGrue)) bought = m.TryBuy(UpgradeType.VitesseGrue);
            deliveries = m.Data.delivered;
        }
        Check(ships >= 1, "au moins un navire est arrivé");
        Check(deliveries > 0, "des conteneurs ont été livrés");
        Check(bought && m.Level(UpgradeType.VitesseGrue) == 1, "une amélioration a été achetée");
        Check(m.Data.xp > 0 || m.PlayerLevel > 1, "les livraisons rapportent de l'XP");
        Console.WriteLine($"  Partie simulée : {ships} navire(s), {deliveries} livraison(s), argent restant {m.Data.money} $");
    }

    static bool TryServe(PortModel m, double now)
    {
        foreach (var v in m.vehicles)
        {
            if (v.state != VehicleState.EnAttente) continue;
            for (int i = 0; i < v.demands.Count; i++)
            {
                var to = v.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, v.bay);
                foreach (var kind in new[] { LocationKind.Yard, LocationKind.Ship })
                {
                    var g = m.GridOf(kind);
                    for (int s = 0; s < g.SlotCount; s++)
                    {
                        var from = new Location(kind, s);
                        if (m.CanMove(from, to)) { Move(m, from, to, now); return true; }
                    }
                }
            }
        }
        return false;
    }

    static void Move(PortModel m, Location from, Location to, double now)
    {
        var c = m.Pick(from);
        var r = m.Drop(to, c, now);
        Check(r.ok, "dépose valide " + from + " -> " + to);
    }

    static void VehiclePatienceAndNoDeadlock()
    {
        var rng = new Random(3);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        // Parc plein à ras bord : les demandes ne doivent viser que des conteneurs accessibles.
        int id = 1000;
        for (int s = 0; s < m.Data.yard.SlotCount; s++)
            for (int h = 0; h < GameRules.YardMaxHeight; h++)
                m.Data.yard.Place(s, new ContainerInfo(id++, (ContainerColor)(h == GameRules.YardMaxHeight - 1 ? 1 : 4)));
        m.Data.nextShipUnix = now + 1e6;
        var counts = m.AvailableColorCounts();
        Check(counts[(int)ContainerColor.Dore] == 0 && counts[(int)ContainerColor.Bleu] == m.Data.yard.SlotCount, "parc plein : seulement les dessus");

        m.nextVehicleUnix = now;
        var v = m.UpdateVehicles(now, rng);
        Check(v != null, "un véhicule arrive");
        m.MarkArrived(v, now);
        foreach (var d in v.demands) Check(d.color == ContainerColor.Bleu, "demande accessible");
        m.UpdateVehicles(now + v.patienceSeconds + 1, rng);
        Check(v.state == VehicleState.Depart, "le véhicule impatient repart");

        // Dépose refusée : mauvaise couleur, et pas de rechargement du navire.
        var m2 = new PortModel(null, now);
        m2.UpdateShip(now + 10, rng);
        var top = m2.PeekTop(FirstNonEmpty(m2));
        Check(!m2.CanDropAt(new Location(LocationKind.Ship, 0), top, new Location(LocationKind.Yard, 0)), "pas de dépose sur le navire");
    }

    static Location FirstNonEmpty(PortModel m)
    {
        for (int s = 0; s < m.Data.ship.SlotCount; s++) if (m.Data.ship.CanPick(s)) return new Location(LocationKind.Ship, s);
        return Location.None;
    }

    static void PlayerLevels()
    {
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        Check(m.PlayerLevel == 1 && m.Data.xp == 0, "nouvelle partie au niveau 1");
        for (int l = 1; l < LevelRules.MaxLevel - 1; l++)
            Check(LevelRules.XpToNext(l + 1) > LevelRules.XpToNext(l), $"seuil d'XP croissant {l}");
        Check(LevelRules.XpFor(ContainerColor.Dore, true) > LevelRules.XpFor(ContainerColor.Dore, false), "bonus XP du train");

        var ups = new List<LevelUpInfo>();
        m.LevelUp += ups.Add;
        long money0 = m.Data.money;
        int need = LevelRules.XpToNext(1);
        Check(m.AddXp(need - 1) == 0 && m.PlayerLevel == 1, "pas de niveau avant le seuil");
        Check(m.AddXp(1) == 1 && m.PlayerLevel == 2 && m.Data.xp == 0, "passage au niveau 2 au seuil exact");
        Check(ups.Count == 1 && ups[0].reward == LevelRules.LevelUpReward(2) && m.Data.money == money0 + ups[0].reward, "prime de niveau versée");
        Check(ups[0].unlockedColor == (int)ContainerColor.Vert, "le vert se débloque au niveau 2");
        Check(m.AddXp(10_000_000) > 1 && m.IsMaxLevel && m.Data.xp == 0, "plusieurs niveaux d'un coup, plafonné au max");
        Check(m.AddXp(50) == 0, "plus d'XP au niveau max");

        var rng = new Random(9);
        bool locked = true;
        for (int i = 0; i < 2000; i++) if (LevelRules.RollColor(rng, 1) > ContainerColor.Bleu) locked = false;
        Check(locked, "niveau 1 : seulement blanc et bleu");
        bool gold = false;
        for (int i = 0; i < 2000; i++) if (LevelRules.RollColor(rng, 6) == ContainerColor.Dore) gold = true;
        Check(gold, "niveau 6 : le doré apparaît");

        var d = SaveData.NewGame(now);
        d.level = 0; d.xp = -3;
        d.Sanitize(now);
        Check(d.level == 1 && d.xp == 0, "niveau réparé à la sanitisation");
    }

    /// <summary>Au passage de niveau, le parc et le navire repartent de zéro (argent et améliorations gardés).</summary>
    static void LevelUpResetsTerminal()
    {
        var rng = new Random(11);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        m.UpdateShip(now + 2, rng);
        // Ranger quelques conteneurs dans le parc.
        for (int i = 0; i < 3; i++)
        {
            var from = FirstNonEmpty(m);
            Move(m, from, new Location(LocationKind.Yard, i), now);
        }
        m.Data.upgrades[0] = 2;
        // Un camion qui demande la couleur du dessus d'une pile du parc.
        m.nextVehicleUnix = now;
        Vehicle v = null;
        for (int t = 0; t < 50 && v == null; t++) v = m.UpdateVehicles(now, rng);
        Check(v != null, "un véhicule arrive");
        m.MarkArrived(v, now);
        Location src = Location.None, dst = Location.None;
        for (int i = 0; i < v.demands.Count && src.IsNone; i++)
        {
            var to = v.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, v.bay);
            foreach (var kind in new[] { LocationKind.Yard, LocationKind.Ship })
                for (int s = 0; s < m.GridOf(kind).SlotCount && src.IsNone; s++)
                {
                    var f = new Location(kind, s);
                    if (m.CanMove(f, to)) { src = f; dst = to; }
                }
        }
        Check(!src.IsNone, "une livraison est possible");
        int resets = 0;
        m.TerminalReset += () => resets++;
        // XP juste sous le seuil : la livraison fait passer le niveau.
        m.Data.xp = LevelRules.XpToNext(1) - 1;
        var c = m.Pick(src);
        var r = m.Drop(dst, c, now);
        Check(r.ok && r.levelsGained == 1 && m.PlayerLevel == 2, "livraison => niveau 2");
        Check(resets == 1, "remise à zéro déclenchée");
        Check(m.Data.yard.TotalCount() == 0 && m.Data.ship.TotalCount() == 0 && !m.Data.shipDocked, "parc et navire vides");
        Check(m.Data.upgrades[0] == 2 && m.Data.money > 0, "argent et améliorations conservés");
        foreach (var veh in m.vehicles) Check(veh.state == VehicleState.Depart, "tous les véhicules repartent");
        Check(m.UpdateShip(now + 5, rng) && m.Data.ship.TotalCount() > 0, "un nouveau navire arrive ensuite");
    }

    /// <summary>Le niveau n'est validé qu'une fois les camions et trains présents servis (ou partis).</summary>
    static void LevelUpWaitsForVehicles()
    {
        var rng = new Random(21);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        m.UpdateShip(now + 2, rng);
        for (int i = 0; i < 4; i++) Move(m, FirstNonEmpty(m), new Location(LocationKind.Yard, i), now);
        // Deux véhicules à quai.
        var present = new List<Vehicle>();
        for (int t = 0; t < 200 && present.Count < 2; t++)
        {
            m.nextVehicleUnix = now;
            var v = m.UpdateVehicles(now, rng);
            if (v != null) { m.MarkArrived(v, now); present.Add(v); }
        }
        Check(present.Count == 2, "deux véhicules présents");
        int ups = 0;
        m.LevelUp += _ => ups++;
        Check(m.AddXp(LevelRules.XpToNext(1) + 1) == 0 && m.PlayerLevel == 1, "XP pleine mais véhicules présents : niveau en attente");
        Check(m.LevelUpPending && m.VehiclesWaiting, "attente signalée");
        // Pas de nouveau véhicule tant que le niveau est en attente.
        m.nextVehicleUnix = now;
        Check(m.UpdateVehicles(now + 0.5, rng) == null, "aucun nouveau véhicule pendant l'attente");
        // Les véhicules repartent (patience écoulée) : le niveau est alors validé.
        foreach (var v in present) v.deadlineUnix = now;
        m.UpdateVehicles(now + 1, rng);
        Check(ups == 1 && m.PlayerLevel == 2 && !m.LevelUpPending, "niveau validé quand plus aucun véhicule n'attend");
        Check(m.Data.yard.TotalCount() == 0, "terminal remis à zéro après validation");
    }

    /// <summary>Musique : boucle déterministe, sans saturation, avec un raccord sans claquement.</summary>
    static void Music()
    {
        const int rate = 22050;
        foreach (MusicStyle style in Enum.GetValues(typeof(MusicStyle)))
        {
            string n = MusicSynth.StyleNames[(int)style];
            var a = MusicSynth.Render(rate, style);
            var b = MusicSynth.Render(rate, style);
            Check(a.Length == MusicSynth.SampleCount(rate, style) && a.Length > rate * 12, n + " : durée de la boucle");
            bool same = true, finite = true;
            float max = 0f, sumSq = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) same = false;
                if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) finite = false;
                max = Math.Max(max, Math.Abs(a[i]));
                sumSq += a[i] * a[i];
            }
            Check(same, n + " : identique à chaque lancement");
            Check(finite && max <= 0.81f && max > 0.5f, n + " : niveau maîtrisé, pas de saturation");
            Check(Math.Sqrt(sumSq / a.Length) > 0.05, n + " : pas silencieuse");
            // Raccord de boucle : le saut entre la fin et le début reste de l'ordre d'un pas normal.
            float jump = Math.Abs(a[0] - a[a.Length - 1]);
            float typical = 0f;
            for (int i = 1; i < a.Length; i++) typical = Math.Max(typical, Math.Abs(a[i] - a[i - 1]));
            Check(jump <= typical, n + " : raccord de boucle sans claquement");
        }
    }

    /// <summary>Pause (décalage des échéances) et amélioration "Revenus".</summary>
    static void PauseAndIncome()
    {
        var rng = new Random(5);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        m.Data.nextShipUnix = now + 30;
        m.Data.turboEndUnix = now + 60;
        m.nextVehicleUnix = now + 10;
        double lastDaily = m.Data.lastDailyChestUnix;
        m.ShiftTimers(100);
        Check(m.Data.nextShipUnix == now + 130 && m.Data.turboEndUnix == now + 160 && m.nextVehicleUnix == now + 110, "pause : échéances décalées");
        Check(m.Data.lastDailyChestUnix == lastDaily, "pause : les coffres suivent l'heure réelle");
        Check(!m.UpdateShip(now + 100, rng), "pause : le navire n'est pas arrivé pendant la pause");

        // Revenus : +10 % par niveau sur les livraisons.
        Check(Math.Abs(GameRules.IncomeMultiplier(0) - 1.0) < 1e-9 && Math.Abs(GameRules.IncomeMultiplier(10) - 2.0) < 1e-9, "revenus : x1 à x2");
        Check(GameRules.EffectLabel(UpgradeType.Revenus, 3) == "+30 %", "revenus : libellé");
        Check(GameRules.UpgradeCost(UpgradeType.Revenus, 1) > GameRules.UpgradeCost(UpgradeType.Revenus, 0), "revenus : coût croissant");

        double t = now;
        var a = new PortModel(null, t);
        var b = new PortModel(null, t);
        b.Data.upgrades[(int)UpgradeType.Revenus] = 5;
        var ra = new Random(3); var rb = new Random(3);
        a.UpdateShip(t + 2, ra); b.UpdateShip(t + 2, rb);
        a.nextVehicleUnix = t; b.nextVehicleUnix = t;
        Vehicle va = null, vb = null;
        for (int i = 0; i < 50 && va == null; i++) { va = a.UpdateVehicles(t, ra); vb = b.UpdateVehicles(t, rb); }
        Check(va != null && vb != null, "revenus : véhicules présents");
        a.MarkArrived(va, t); b.MarkArrived(vb, t);
        long ga = DeliverAny(a, va, t + 20), gb = DeliverAny(b, vb, t + 20);
        Check(ga > 0 && Math.Abs(gb - ga * 1.5) <= 1, "revenus : +50 % au niveau 5 (" + ga + " -> " + gb + ")");
    }

    /// <summary>Livre au véhicule le premier conteneur possible ; renvoie le gain.</summary>
    static long DeliverAny(PortModel m, Vehicle v, double now)
    {
        for (int i = 0; i < v.demands.Count; i++)
        {
            var to = v.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, v.bay);
            for (int s = 0; s < m.Data.ship.SlotCount; s++)
            {
                var f = new Location(LocationKind.Ship, s);
                if (!m.CanMove(f, to)) continue;
                var c = m.Pick(f);
                var r = m.Drop(to, c, now);
                return r.ok ? r.earned : 0;
            }
        }
        return 0;
    }

    static Location Y(int slot) => new Location(LocationKind.Yard, slot);

    static void NewStackingRules()
    {
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        var small = new ContainerInfo(1, ContainerColor.Bleu, 20);
        var big = new ContainerInfo(2, ContainerColor.Bleu, 40);
        Check(m.Drop(Y(0), small, now).ok, "20 pieds au sol");
        Check(m.CheckYardPlace(0, big) == PlaceError.QuaranteSurVingt, "40 pieds interdit sur un 20 pieds");
        Check(m.Drop(Y(1), big, now).ok && m.CheckYardPlace(1, new ContainerInfo(3, ContainerColor.Blanc, 20)) == PlaceError.Aucune, "20 pieds autorisé sur un 40 pieds");

        var hz = new ContainerInfo(4, ContainerColor.Rouge, 40, hazard: true);
        Check(m.Drop(Y(7), hz, now).ok, "dangereux posé");
        var hz2 = new ContainerInfo(5, ContainerColor.Rouge, 40, hazard: true);
        Check(m.CheckYardPlace(8, hz2) == PlaceError.Dangereux, "dangereux interdit à côté (droite)");
        Check(m.CheckYardPlace(2, hz2) == PlaceError.Dangereux, "dangereux interdit à côté (dessus)");
        Check(m.CheckYardPlace(7, hz2) == PlaceError.Dangereux, "dangereux interdit sur un dangereux");
        Check(m.CheckYardPlace(13, hz2) == PlaceError.Aucune, "dangereux autorisé en diagonale");
        Check(m.CheckYardPlace(8, new ContainerInfo(6, ContainerColor.Blanc)) == PlaceError.Aucune, "conteneur normal à côté d'un dangereux");

        int lockedSlot = GameRules.YardColumns * 3; // 4e rangée
        Check(m.CheckYardPlace(lockedSlot, big) == PlaceError.Verrouille, "4e rangée verrouillée au départ");
        for (int i = 0; i < 3; i++) m.Data.yard.Place(10, new ContainerInfo(100 + i, ContainerColor.Blanc));
        Check(m.CheckYardPlace(10, big) == PlaceError.PilePleine, "hauteur 3 au départ");
        m.Data.money = 1_000_000;
        Check(m.TryBuy(UpgradeType.AgrandirParc) && m.CheckYardPlace(lockedSlot, big) == PlaceError.Aucune, "agrandissement : 4e rangée");
        Check(m.TryBuy(UpgradeType.AgrandirParc) && m.CheckYardPlace(10, big) == PlaceError.Aucune, "agrandissement : hauteur 4");
        Check(!m.CanBuy(UpgradeType.AgrandirParc), "parc au maximum");
        Check(!m.HasSecondCrane && m.TryBuy(UpgradeType.DeuxiemeGrue) && m.HasSecondCrane && !m.CanBuy(UpgradeType.DeuxiemeGrue), "2e grue achetée une seule fois");

        // Navires : jamais de 40 pieds posé sur un 20 pieds dans les cales.
        var rng = new Random(5);
        var m2 = new PortModel(null, now);
        m2.Data.level = 12;
        for (int k = 0; k < 50; k++)
        {
            m2.Data.shipDocked = false; m2.Data.nextShipUnix = 0;
            m2.UpdateShip(now, rng);
            var g = m2.Data.ship;
            for (int sl = 0; sl < g.SlotCount; sl++)
                for (int t = 1; t < g.Height(sl); t++)
                    Check(!(g.At(sl, t - 1).IsSmall && !g.At(sl, t).IsSmall), "cale : pas de 40 sur 20");
        }
    }

    static void ReefersAndBonus()
    {
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        var cold = new ContainerInfo(1, ContainerColor.Vert, 40, reefer: true);
        var plugged = new ContainerInfo(2, ContainerColor.Vert, 40, reefer: true);
        m.Drop(Y(GameRules.YardColumns + 1), cold, now);   // 2e rangée : pas de prise
        m.Drop(Y(1), plugged, now);                          // 1re rangée : électrifiée
        int v0 = GameRules.ValueOf(cold);
        for (int i = 0; i < 30; i++) m.UpdateReefers(1f);
        Check(GameRules.ValueOf(cold) < v0, "frigorifique débranché : valeur en baisse");
        Check(GameRules.ValueOf(plugged) == v0, "frigorifique branché : valeur intacte");
        for (int i = 0; i < 500; i++) m.UpdateReefers(1f);
        Check(GameRules.Freshness(cold) >= GameRules.ReeferMinFactor - 1e-6, "valeur minimale garantie");
        Check(GameRules.ValueOf(new ContainerInfo(3, ContainerColor.Dore, 20)) < GameRules.ValueOf(new ContainerInfo(4, ContainerColor.Dore, 40)), "20 pieds vaut moins");
        var basic = new ContainerInfo(5, ContainerColor.Rouge);
        Check(GameRules.Payout(basic, false, true) == (int)Math.Round(100 * GameRules.FastBonus), "bonus rapidité +20 %");
    }

    static void UpcomingAndContracts()
    {
        var rng = new Random(21);
        double now = 1_700_000_000;
        var m = new PortModel(null, now);
        m.UpdateShip(now + 2, rng);
        m.nextVehicleUnix = now + 1000;
        m.UpdateVehicles(now, rng);
        Check(m.upcoming.Count == GameRules.UpcomingCount, "3 véhicules annoncés");
        var firstColors = new List<ContainerColor>(m.upcoming[0].colors);
        var kind = m.upcoming[0].kind;
        m.nextVehicleUnix = now;
        var v = m.UpdateVehicles(now, rng);
        Check(v != null && v.kind == kind, "le véhicule annoncé arrive");
        if (v != null && v.demands.Count == firstColors.Count)
            for (int i = 0; i < firstColors.Count; i++) Check(v.demands[i].color == firstColors[i], "couleur annoncée respectée");
        Check(m.upcoming.Count == GameRules.UpcomingCount - 1, "annonce consommée");
        m.UpdateVehicles(now, rng);
        Check(m.upcoming.Count == GameRules.UpcomingCount, "annonces complétées");

        Check(m.EnsureContracts(now, rng) && m.Data.contracts.Count == ContractRules.Count, "3 contrats du jour");
        Check(!m.EnsureContracts(now + 60, rng), "même jour : mêmes contrats");
        Check(m.EnsureContracts(now + 86400, rng), "jour suivant : nouveaux contrats");
        // Contrat sur mesure : 2 conteneurs bleus.
        m.Data.contracts[0] = new Contract { color = (int)ContainerColor.Bleu, target = 2, rewardMoney = 500, rewardXp = 1 };
        var done = new List<Contract>();
        m.ContractCompleted += done.Add;
        Check(ContractRules.Matches(m.Data.contracts[0], new ContainerInfo(1, ContainerColor.Bleu), false, false), "contrat : bonne couleur");
        Check(!ContractRules.Matches(m.Data.contracts[0], new ContainerInfo(1, ContainerColor.Rouge), false, false), "contrat : mauvaise couleur");
        Check(!ContractRules.Matches(new Contract { vehicle = 1, target = 1 }, new ContainerInfo(1, ContainerColor.Bleu), false, false), "contrat train : camion refusé");
        foreach (var c in m.Data.contracts) Check(c.Describe().StartsWith("Livrer "), "description lisible");

        // Livraison réelle de deux bleus via des camions.
        int delivered = 0;
        for (int step = 0; step < 4000 && delivered < 2; step++)
        {
            now += 1;
            m.UpdateShip(now, rng);
            var nv = m.UpdateVehicles(now, rng);
            if (nv != null) m.MarkArrived(nv, now);
            foreach (var veh in m.vehicles.ToArray()) if (veh.state == VehicleState.Depart) m.RemoveVehicle(veh);
            foreach (var veh in m.vehicles)
            {
                if (veh.state != VehicleState.EnAttente) continue;
                for (int i = 0; i < veh.demands.Count; i++)
                {
                    if (veh.demands[i].color != ContainerColor.Bleu) continue;
                    var to = veh.kind == VehicleKind.Train ? new Location(LocationKind.Wagon, i) : new Location(LocationKind.Truck, veh.bay);
                    for (int sl = 0; sl < m.Data.ship.SlotCount; sl++)
                    {
                        var from = new Location(LocationKind.Ship, sl);
                        if (m.CanMove(from, to)) { m.Drop(to, m.Pick(from), now); delivered++; goto next; }
                    }
                }
            }
            if (m.ShipNeedsDeparture) m.DepartShip(now);
            if (!m.Data.shipDocked) m.Data.nextShipUnix = Math.Min(m.Data.nextShipUnix, now);
            next:;
        }
        Check(delivered >= 2 && done.Count >= 1 && m.Data.contracts[0].done, "contrat rempli après 2 bleus");
    }

    static void AmbienceThemesRecords()
    {
        double t0 = 1_700_000_000;
        bool day = false, night = false;
        for (int i = 0; i < 600; i++)
        {
            float n = Ambience.NightAmount(t0 + i);
            Check(n >= 0 && n <= 1, "nuit entre 0 et 1");
            if (n < 0.01f) day = true;
            if (n > 0.99f) night = true;
        }
        Check(day && night, "le cycle contient un jour et une nuit");
        Check(Ambience.WeatherAt(t0) == Ambience.WeatherAt(t0 + 1), "météo stable sur un bloc");
        var seen = new HashSet<Weather>();
        for (int i = 0; i < 300; i++) seen.Add(Ambience.WeatherAt(t0 + i * Ambience.WeatherBlockSeconds));
        Check(seen.Count == 3, "les trois météos existent");
        Check(Ambience.CraneFactor(Weather.Vent) < 1f, "le vent ralentit la grue");

        Check(LevelRules.ThemeFor(1) == PortTheme.Tempere && LevelRules.ThemeFor(10) == PortTheme.Tropical && LevelRules.ThemeFor(25) == PortTheme.Polaire, "ports à thème");
        Check(LevelRules.UnlocksAt(3).Contains("frigorifiques") && LevelRules.UnlocksAt(10).Contains("tropical"), "nouveautés annoncées");

        var rng = new Random(2);
        var m = new PortModel(null, t0);
        m.UpdateShip(t0 + 2, rng);
        while (!m.Data.ship.IsEmpty)
            for (int sl = 0; sl < m.Data.ship.SlotCount; sl++) m.Data.ship.Pick(sl);
        m.DepartShip(t0 + 62);
        Check(Math.Abs(m.Data.bestUnloadSeconds - 60) < 1e-6 && m.Data.shipsUnloaded == 1, "record de déchargement");
    }
}
