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
            for (int l = 0; l < GameRules.UpgradeMaxLevel - 1; l++)
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
        Check(d.upgrades.Length == 3 && d.upgrades[0] == GameRules.UpgradeMaxLevel, "niveaux réparés");
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
}
