using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class CultivatorCombatRegressionTests
{
    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(true, 100)]
    [DataRow(false, 0)]
    [DataRow(false, 100)]
    public void OneNascentSoulDefeatsTenGoldenCoresDespiteLowerAggregatePower(bool higherAttacks, int fatigue)
    {
        for (var seed = 1; seed <= 16; seed++)
        {
            var world = CreateBattleWorld(seed, higherAttacks ? 3 : 2, higherAttacks ? 2 : 15, higherAttacks ? 2 : 3, higherAttacks ? 10 : 1);
            var attacker = world.Player;
            var defender = world.Sects.Single(s => s.Id == "tiangong");
            var higher = higherAttacks ? attacker : defender;
            var lower = higherAttacks ? defender : attacker;
            if (higherAttacks) GameEngine.Members(world, higher).Last().Realm = 0; // One reserve stays home.
            higher.WarExhaustion = fatigue;
            Assert.IsLessThan(GameEngine.Power(world, lower), GameEngine.Power(world, higher), "高境一方的展示总战力仍应更低。");
            var campaign = PrepareBattle(world);
            Assert.HasCount(higherAttacks ? 1 : 10, campaign.Participants);
            var higherId = GameEngine.Members(world, higher).Single(p => p.Realm == 3).Id;
            var lowerIds = higherAttacks ? GameEngine.Members(world, lower).Select(p => p.Id).ToArray() : campaign.Participants.ToArray();

            GameEngine.AdvanceMonth(world, QuietDecisions(world));

            var report = world.BattleReports.Single();
            Assert.AreEqual(higherAttacks, report.AttackerWon, $"seed={seed}, fatigue={fatigue}");
            Assert.IsLessThan(48, report.Rounds, "不得用48轮僵持冒充高境胜利。");
            Assert.IsLessThan(800d, report.Exchanges.Where(e => e.DefenderId == higherId).Sum(e => e.Damage));
            foreach (var id in lowerIds)
                Assert.IsGreaterThanOrEqualTo(400d, report.Exchanges.Where(e => e.DefenderId == id).Sum(e => e.Damage), "十名参战金丹都应失去站立能力。");
            Assert.IsGreaterThan(report.Rounds, report.Exchanges.Where(e => e.AttackerId == higherId).Sum(e => e.Hits), "高境修士应实际攻击多个低境目标。");
            foreach (var id in lowerIds)
                Assert.IsLessThanOrEqualTo(report.Rounds, report.Exchanges.Where(e => e.AttackerId == id).Sum(e => e.Hits), "低境方不能对高境目标群攻。");
            GameEngine.Validate(world);
        }
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(4)]
    public void EqualOrHigherRealmTargetsRemainSingleTargetPerRound(int defenderRealm)
    {
        var world = CreateBattleWorld(42, 3, 2, defenderRealm, 3);
        GameEngine.Members(world, world.Player).Last().Realm = 0;
        var participant = PrepareBattle(world).Participants.Single();
        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        var report = world.BattleReports.Single();
        Assert.IsLessThanOrEqualTo(report.Rounds, report.Exchanges.Where(e => e.AttackerId == participant).Sum(e => e.Hits));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void ASubduedTargetCannotBeHitRepeatedlyWithinTheSameRound()
    {
        var world = CreateBattleWorld(42, 3, 2, 2, 1);
        GameEngine.Members(world, world.Player).Last().Realm = 0;
        var participant = PrepareBattle(world).Participants.Single();
        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        var report = world.BattleReports.Single();
        Assert.IsGreaterThanOrEqualTo(2, report.Rounds, "单个金丹不能因四个攻击名额被同轮重复击中。");
        Assert.IsLessThanOrEqualTo(report.Rounds, report.Exchanges.Single(e => e.AttackerId == participant).Hits);
        GameEngine.Validate(world);
    }

    [TestMethod]
    [DataRow("injury")]
    [DataRow("formation")]
    [DataRow("supply")]
    public void RealmAdvantageDoesNotGuaranteeVictoryThroughSevereDisadvantages(string disadvantage)
    {
        var world = CreateBattleWorld(42, 3, 2, 2, 10);
        GameEngine.Members(world, world.Player).Last().Realm = 0;
        if (disadvantage == "formation") world.Regions.Single(r => r.Id == "r11").Fort = 5;
        var campaign = PrepareBattle(world);
        if (disadvantage == "injury") world.People.Single(p => p.Id == campaign.Participants.Single()).Injury = 95;
        if (disadvantage == "formation") campaign.DefenseBonus = 1.45;
        if (disadvantage == "supply") { campaign.Supply = .5; world.Player.WarExhaustion = 100; }
        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        Assert.IsFalse(world.BattleReports.Single().AttackerWon, disadvantage);
        Assert.AreEqual("tiangong", world.Regions.Single(r => r.Id == "r11").Owner);
        GameEngine.Validate(world);
    }

    [TestMethod]
    [DataRow(3, 2, true)]
    [DataRow(2, 3, false)]
    public void AutomaticCampaignAssessmentUsesRealmMatchupsInsteadOfAggregatePower(int attackerRealm, int defenderRealm, bool shouldAttack)
    {
        var world = CreateBattleWorld(42, attackerRealm, 2, defenderRealm, 10);
        var attacker = world.Player;
        GameEngine.Members(world, attacker).Last().Realm = 0;
        EnableRivalCampaign(world, attacker);
        Assert.IsLessThan(GameEngine.Power(world, world.Sects.Single(s => s.Id == "tiangong")) * .8, GameEngine.Power(world, attacker));
        GameEngine.AdvanceMonth(world, QuietDecisions(world, attacker.Id));
        Assert.AreEqual(shouldAttack, world.Campaigns.Any(c => c.Attacker == attacker.Id));
        if (shouldAttack)
            Assert.HasCount(1, world.Campaigns.Single(c => c.Attacker == attacker.Id).Participants);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AnAbsentHighRealmCultivatorCannotJustifyAnAutomaticCampaign()
    {
        var world = CreateBattleWorld(42, 3, 4, 2, 10);
        var attacker = world.Player;
        var members = GameEngine.Members(world, attacker);
        foreach (var person in members.Skip(1)) person.Realm = 0;
        var origin = GameEngine.Log(world, "journey", "隔离测试历练", "高境修士正在外历练，不能加入远征。", [attacker.Id]);
        world.Expeditions.Add(new Expedition { Id = world.NewId(), DiscipleId = members[0].Id, SectId = attacker.Id, RegionId = "r10", MonthsRemaining = 12, OriginEvent = origin });
        EnableRivalCampaign(world, attacker);
        GameEngine.AdvanceMonth(world, QuietDecisions(world, attacker.Id));
        Assert.IsFalse(world.Campaigns.Any(c => c.Attacker == attacker.Id));
        GameEngine.Validate(world);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(3, true)]
    public void AutomaticCampaignAssessmentIncludesNeutralLocalGuards(int attackerRealm, bool shouldAttack)
    {
        var world = CreateBattleWorld(42, attackerRealm, 2, 2, 10);
        var attacker = world.Player;
        var target = world.Regions.Single(r => r.Id == "r10");
        target.Owner = ""; target.Fort = 5;
        EnableRivalCampaign(world, attacker);

        GameEngine.AdvanceMonth(world, QuietDecisions(world, attacker.Id, target.Id));

        Assert.AreEqual(shouldAttack, world.Campaigns.Any(c => c.Attacker == attacker.Id && c.Target == target.Id),
            "无主地也有地方守卫，弱军不应跳过评估，高境可调动门人仍应能出征。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void HigherRealmStrikeHitsOnlyFourDistinctTargetsInOneRound()
    {
        var world = CreateBattleWorld(42, 3, 2, 2, 10);
        GameEngine.Members(world, world.Player).Last().Realm = 0;
        world.Sects.Single(s => s.Id == "tiangong").Training = 10;
        world.Regions.Single(r => r.Id == "r11").Fort = 5;
        var participant = PrepareBattle(world).Participants.Single();
        world.People.Single(p => p.Id == participant).Injury = 95;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        var report = world.BattleReports.Single();
        Assert.AreEqual(1, report.Rounds, "重伤高境修士应在同轮反击后失去站立能力，使四目标上限可直接观察。");
        var strikes = report.Exchanges.Where(e => e.AttackerId == participant).ToList();
        Assert.HasCount(4, strikes);
        Assert.HasCount(4, strikes.Select(e => e.DefenderId).Distinct().ToList());
        Assert.IsTrue(strikes.All(e => e.Hits == 1 && e.DefenderRealm < 3));
        GameEngine.Validate(world);
    }

    private static GameState CreateBattleWorld(int seed, int attackerRealm, int attackers, int defenderRealm, int defenders)
    {
        var world = WorldFactory.Create("baicao", seed, "rules", "normal", compact: true);
        GameEngine.Command(world, new GameCommand("choose", Option: "prosperity"));
        world.AutoStewardship = false;
        world.NextEventMonth = 10000;
        foreach (var sect in world.Sects)
        {
            sect.Stones = 0; sect.Herbs = 100000; sect.Stability = 80;
            sect.Training = 1; sect.Intel = 0; sect.WarExhaustion = 0;
        }
        ConfigureArmy(world, world.Player, attackerRealm, attackers);
        var defender = world.Sects.Single(s => s.Id == "tiangong");
        ConfigureArmy(world, defender, defenderRealm, defenders);
        world.Player.Stones = 100000;
        defender.Stones = 200;
        world.Regions.Single(r => r.Id == "r10").Owner = world.PlayerId;
        world.Regions.Single(r => r.Id == "r11").Fort = 1;
        GameEngine.Validate(world);
        return world;
    }

    private static void ConfigureArmy(GameState world, Sect sect, int realm, int count)
    {
        var people = world.People.Where(p => p.SectId == sect.Id).OrderBy(p => p.Id).ToList();
        while (people.Count < count) people.Add(WorldFactory.NewCultivator(world, sect.Id));
        for (var i = 0; i < people.Count; i++)
        {
            var person = people[i];
            person.Alive = i < count;
            person.Realm = realm; person.Potential = 55; person.Injury = 0;
            person.Progress = 0; person.RealmMonths = 0; person.AgeMonths = 240;
        }
        sect.LeaderId = people[0].Id;
    }

    private static Campaign PrepareBattle(GameState world)
    {
        GameEngine.Command(world, new GameCommand("attack", "r11"));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));
        var campaign = world.Campaigns.Single();
        campaign.MonthsRemaining = 1; // Exercise real settlement without waiting through travel.
        return campaign;
    }

    private static void EnableRivalCampaign(GameState world, Sect attacker)
    {
        world.PlayerId = "qingyun";
        attacker.Stones = 200; // Enough to mobilize, not enough to auto-recruit before the assessment.
        world.RandomState = 1; // Deterministic aggression and declaration draws.
    }

    private static List<AgentDecision> QuietDecisions(GameState world, string? attackerId = null, string targetId = "r11") => world.Sects
        .Where(s => s.Id != world.PlayerId && !s.Eliminated).Select(s => new AgentDecision
        {
            SectId = s.Id, Policy = s.Id == attackerId ? "expansion" : "balanced",
            Target = s.Id == attackerId ? targetId : "", Reason = "隔离境界与出征评估回归。"
        }).ToList();
}
