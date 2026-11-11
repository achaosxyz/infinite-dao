using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class CultivatorCombatRegressionTests
{
    [TestMethod]
    public void GarrisonAssessmentUsesTheSamePersonalAttackBonusWithoutRandomRolls()
    {
        var plain = CreateBattleWorld(43, 1, 1, 1, 1); var armed = plain.Copy();
        var person = GameEngine.Members(armed, armed.Player).Single();
        var relic = new Relic { Id = armed.NewId(), Name = "赤霞火印", Scope = "personal", TemplateId = "attack_2_1", Owner = armed.PlayerId, HolderId = person.Id, RegionId = armed.Player.Capital, FoundMonth = armed.Month };
        armed.Relics.Add(relic);
        var root = GameEngine.Log(armed, "relic_discovery", "得宝", person.Name + "得宝", [armed.PlayerId]);
        armed.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToHolderId = person.Id, ToOwner = armed.PlayerId, RegionId = relic.RegionId };
        relic.OriginEvent = relic.LastEventId = root;
        var assess = typeof(GameEngine).GetMethod("CanChallengeGarrison", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        bool Can(GameState state) => (bool)assess.Invoke(null, [state, state.Player, state.Regions.Single(r => r.Id == "r11"), GameEngine.Members(state, state.Player)])!;
        var randomBefore = armed.RandomState; var foundThreshold = false;
        for (var fort = 0; fort <= 5 && !foundThreshold; fort++)
        for (var injury = 0; injury <= 95 && !foundThreshold; injury++)
        {
            plain.Regions.Single(r => r.Id == "r11").Fort = fort; armed.Regions.Single(r => r.Id == "r11").Fort = fort;
            GameEngine.Members(plain, plain.Player).Single().Injury = injury; person.Injury = injury;
            foundThreshold = !Can(plain) && Can(armed);
        }
        Assert.IsTrue(foundThreshold, "必须存在御敌法宝改变真实阵容出征估算的边界");
        Assert.AreEqual(randomBefore, armed.RandomState, "出征估算不能消耗规则随机数");
    }

    [TestMethod]
    [DataRow("传承惊雷尺", "传承惊雷尺")]
    [DataRow("", "无名法宝")]
    [DataRow(null, "无名法宝")]
    [DataRow("  ", "无名法宝")]
    public void PersonalAttackRelicChangesOnlyHolderDamageAndBattleSnapshotPersists(string? instanceName, string expectedSnapshot)
    {
        var plain = CreateBattleWorld(42, 3, 2, 0, 1);
        GameEngine.Members(plain, plain.Player).Last().Realm = 0;
        var participant = PrepareBattle(plain).Participants.Single();
        var armed = plain.Copy();
        var relic = new Relic { Id = armed.NewId(), Name = instanceName!, Scope = "personal", TemplateId = "attack_3_1", Owner = armed.PlayerId, HolderId = participant, RegionId = armed.Player.Capital, FoundMonth = armed.Month };
        armed.Relics.Add(relic);
        var origin = GameEngine.Log(armed, "relic_discovery", "得宝", "得宝", [armed.PlayerId]);
        armed.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = armed.PlayerId, ToHolderId = participant, RegionId = relic.RegionId };
        relic.OriginEvent = relic.LastEventId = origin;
        GameEngine.Validate(armed);
        static BattleReport Fight(GameState state)
        {
            var campaign = state.Campaigns.Single();
            var method = typeof(GameEngine).GetMethod("ResolveCultivatorBattle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            return (BattleReport)method.Invoke(null, [state, state.Player, state.Regions.Single(r => r.Id == campaign.Target), "balanced", 1d, 1d, campaign.Participants])!;
        }
        var plainReport = Fight(plain); var armedReport = Fight(armed);
        var baseline = plainReport.Exchanges.First(e => e.AttackerId == participant);
        var snapshot = armedReport.Exchanges.Single(e => e.AttackerId == participant && e.DefenderId == baseline.DefenderId);
        Assert.AreEqual("", baseline.RelicName); Assert.AreEqual("", baseline.RelicCategory);
        Assert.AreEqual(1, baseline.Hits); Assert.AreEqual(1, snapshot.Hits);
        Assert.AreEqual(baseline.Damage * 1.16, snapshot.Damage, 0.000001);
        Assert.AreEqual(expectedSnapshot, snapshot.RelicName);
        Assert.AreEqual(.16, snapshot.RelicBonus, 0.000001);
        relic.Name = "后来更改的显示名称";
        var restored = System.Text.Json.JsonSerializer.Deserialize<BattleReport>(System.Text.Json.JsonSerializer.Serialize(armedReport))!;
        Assert.AreEqual(expectedSnapshot, restored.Exchanges.Single(e => e.AttackerId == participant && e.DefenderId == baseline.DefenderId).RelicName);
    }

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
            person.Realm = realm; person.Potential = 55; person.Injury = 0; person.InnateTrait = 0;
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
