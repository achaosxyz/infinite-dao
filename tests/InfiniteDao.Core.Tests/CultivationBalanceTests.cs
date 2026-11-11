using System.Reflection;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class CultivationBalanceTests
{
    [TestMethod]
    public void MahayanaAt120MonthsAscendsByDefaultAddsFoundationAndElectsSuccessor()
    {
        CollectionAssert.AreEqual(new[] { 28, 36, 48, 62, 78, 94, 110, 130 }, WorldFactory.Lifespans);
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var ascendant = members.Single(p => p.Id == sect.LeaderId);
        ascendant.RealmMonths = 119;
        ascendant.AscensionWarned = true;
        sect.Foundation = 9;
        Assert.AreEqual(1.45, GameEngine.FoundationMultiplier(sect), 0.000001);
        sect.Foundation = 10;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsFalse(ascendant.Alive);
        Assert.IsTrue(ascendant.Ascended);
        Assert.IsFalse(ascendant.RetainInWorld);
        Assert.AreEqual(120, ascendant.RealmMonths);
        Assert.AreEqual(130 * 12, GameEngine.MaximumAgeMonths(ascendant));
        Assert.AreEqual(11, sect.Foundation);
        Assert.AreEqual(1.5, GameEngine.FoundationMultiplier(sect), 0.000001);
        Assert.AreNotEqual(ascendant.Id, sect.LeaderId);
        var successor = GameEngine.Members(world, sect).Single(p => p.Id == sect.LeaderId);
        Assert.AreEqual("掌门", successor.Role);
        Assert.IsTrue(world.History.Any(e => e.Kind == "ascension" && e.Title == $"{ascendant.Name}飞升"));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AscensionWarningAt108AllowsRetainWithoutFeeAndOnlyOnce()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var person = members.Single(p => p.Id == sect.LeaderId);
        person.RealmMonths = 107;
        sect.Stones = 1_000_000;
        sect.Herbs = 1_000_000;
        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        var warning = world.PendingEvent!;
        Assert.AreEqual(108, person.RealmMonths);
        Assert.AreEqual("ascension", warning.Type);
        Assert.AreEqual(person.Id.ToString(), warning.Target);
        Assert.IsTrue(person.AscensionWarned);
        Assert.AreEqual(stonesBefore + stoneIncome - stoneUpkeep, sect.Stones);
        Assert.AreEqual(herbsBefore + herbIncome - herbUpkeep, sect.Herbs);
        var retainChoice = warning.Choices.Single(choice => choice.Id == "retain");
        Assert.AreEqual(0, retainChoice.Stones);
        Assert.AreEqual(0, retainChoice.Herbs);

        var stonesBeforeChoice = sect.Stones;
        var herbsBeforeChoice = sect.Herbs;
        GameEngine.Command(world, new GameCommand("choose", Option: "retain"));
        Assert.IsTrue(person.RetainInWorld);
        Assert.IsTrue(person.AscensionWarned);
        Assert.AreEqual(stonesBeforeChoice, sect.Stones);
        Assert.AreEqual(herbsBeforeChoice, sect.Herbs);

        stoneIncome = GameEngine.Income(world, sect);
        stoneUpkeep = GameEngine.Upkeep(world, sect);
        herbIncome = GameEngine.HerbIncome(world, sect);
        herbUpkeep = GameEngine.HerbUpkeep(world, sect);
        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(109, person.RealmMonths);
        Assert.IsNull(world.PendingEvent, "已作选择的人物不应重复触发飞升议事。");
        Assert.AreEqual(stonesBeforeChoice + stoneIncome - stoneUpkeep, sect.Stones);
        Assert.AreEqual(herbsBeforeChoice + herbIncome - herbUpkeep, sect.Herbs);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void RetentionChargesAfterNormalUpkeepAndInsufficientNextMonthAscends()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var ascendant = members.Single(p => p.Id == sect.LeaderId);
        ascendant.RealmMonths = 1320;
        var target = world.Regions.First(r => r.Owner != sect.Id && GameEngine.Border(world, sect, r));
        sect.Policy = "expansion";
        sect.Target = target.Id;
        sect.Stones = 1_000_000;
        sect.Herbs = 1_000_000;

        GameEngine.Command(world, new GameCommand("retention", ascendant.Id.ToString(), "on"));
        Assert.IsTrue(ascendant.AscensionWarned, "显式留世命令应标记飞升议事已处理。");
        var retentionCost = GameEngine.RetentionCost(world, sect, ascendant);
        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsTrue(ascendant.Alive);
        Assert.IsTrue(ascendant.RetainInWorld);
        Assert.AreEqual(stonesBefore + stoneIncome - stoneUpkeep - retentionCost.Stones, sect.Stones);
        Assert.AreEqual(herbsBefore + herbIncome - herbUpkeep - retentionCost.Herbs, sect.Herbs);
        Assert.IsGreaterThanOrEqualTo(GameEngine.ReservedStones(world, sect), sect.Stones,
            "留世支出不得挪用正常供养与扩张军需储备。");

        var nextCost = GameEngine.RetentionCost(world, sect, ascendant);
        var nextStoneReserve = GameEngine.ReservedStones(world, sect);
        var nextHerbReserve = GameEngine.ReservedHerbs(world, sect);
        var nextStoneIncome = GameEngine.Income(world, sect);
        var nextStoneUpkeep = GameEngine.Upkeep(world, sect);
        var nextHerbIncome = GameEngine.HerbIncome(world, sect);
        var nextHerbUpkeep = GameEngine.HerbUpkeep(world, sect);
        var availableStones = nextStoneReserve + nextCost.Stones - 1;
        var availableHerbs = nextHerbReserve + nextCost.Herbs - 1;
        sect.Stones = availableStones - nextStoneIncome + nextStoneUpkeep;
        sect.Herbs = availableHerbs - nextHerbIncome + nextHerbUpkeep;
        Assert.IsGreaterThanOrEqualTo(nextStoneUpkeep, sect.Stones, "测试资源仍足以支付普通灵石供养。");
        Assert.IsGreaterThanOrEqualTo(nextHerbUpkeep, sect.Herbs, "测试资源仍足以支付普通灵材供养。");

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsFalse(ascendant.Alive);
        Assert.IsTrue(ascendant.Ascended);
        Assert.IsFalse(ascendant.RetainInWorld);
        Assert.AreEqual(availableStones, sect.Stones, "不足留世费用时，应保留供养和军需后自动飞升。");
        Assert.AreEqual(availableHerbs, sect.Herbs, "不足留世费用时，不得扣掉留世资源后才飞升。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void OngoingCampaignReserveIsProtectedFromRetentionWhenStewardshipIsDisabled()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var ascendant = members.Single(p => p.Id == sect.LeaderId);
        ascendant.RealmMonths = 120;
        ascendant.Injury = 60;
        var target = world.Regions.First(r => r.Owner != sect.Id && GameEngine.Border(world, sect, r));

        GameEngine.Command(world, new GameCommand("attack", target.Id));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));
        var campaign = world.Campaigns.Single();
        campaign.MonthsRemaining = 2;
        Assert.DoesNotContain(ascendant.Id, campaign.Participants);
        Assert.IsFalse(world.AutoStewardship);
        world.CommandsLeft = 1;
        GameEngine.Command(world, new GameCommand("retention", ascendant.Id.ToString(), "on"));

        var retentionCost = GameEngine.RetentionCost(world, sect, ascendant);
        var reservedStones = GameEngine.ReservedStones(world, sect);
        var reservedHerbs = GameEngine.ReservedHerbs(world, sect);
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);
        var availableStones = retentionCost.Stones + reservedStones - 1;
        var availableHerbs = retentionCost.Herbs + reservedHerbs - 1;
        sect.Stones = availableStones - stoneIncome + stoneUpkeep;
        sect.Herbs = availableHerbs - herbIncome + herbUpkeep;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsFalse(ascendant.Alive);
        Assert.IsTrue(ascendant.Ascended);
        Assert.IsFalse(ascendant.RetainInWorld);
        Assert.AreEqual(1, campaign.MonthsRemaining, "战役应继续推进而非因留世支出断供。");
        Assert.AreEqual(1d, campaign.Supply, 0.000001);
        Assert.AreEqual(availableStones - campaign.MonthlyStones, sect.Stones);
        Assert.AreEqual(availableHerbs - campaign.MonthlyHerbs, sect.Herbs);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void LifespanDeathPrecedesRetentionWithoutChargingRetentionFee()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var person = members.Single(p => p.Id == sect.LeaderId);
        person.AgeMonths = GameEngine.MaximumAgeMonths(person) - 1;
        person.RealmMonths = 120;
        sect.Foundation = 3;
        GameEngine.Command(world, new GameCommand("retention", person.Id.ToString(), "on"));
        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsFalse(person.Alive);
        Assert.IsFalse(person.Ascended);
        Assert.IsFalse(person.RetainInWorld);
        Assert.AreEqual(3, sect.Foundation);
        Assert.AreEqual(stonesBefore + stoneIncome - stoneUpkeep, sect.Stones);
        Assert.AreEqual(herbsBefore + herbIncome - herbUpkeep, sect.Herbs);
        Assert.IsTrue(world.History.Any(e => e.Kind == "death" && e.Text.Contains(person.Name)));
        Assert.IsFalse(world.History.Any(e => e.Kind == "ascension" && e.Title == $"{person.Name}飞升"));
        GameEngine.Validate(world);
    }

    [TestMethod]
    [DataRow("stones")]
    [DataRow("herbs")]
    public void SectUpkeepShortageBlocksOrdinaryAndFocusedCultivation(string shortage)
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var trainee = members.Single(p => p.Id == sect.LeaderId);
        trainee.Realm = 2;
        trainee.Progress = 20;
        trainee.RealmMonths = 0;
        trainee.Focused = true;
        var progressBefore = trainee.Progress;

        if (shortage == "stones")
        {
            sect.Stones = 0;
            sect.Herbs = 100_000;
            Assert.IsGreaterThan(GameEngine.Income(world, sect), GameEngine.Upkeep(world, sect));
        }
        else
        {
            sect.Stones = 1_000_000;
            sect.Herbs = 0;
            Assert.IsGreaterThan(GameEngine.HerbIncome(world, sect), GameEngine.HerbUpkeep(world, sect));
        }

        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(progressBefore, trainee.Progress, "任何一类全宗供养短缺都不得增加修为。");
        if (shortage == "stones")
            Assert.AreEqual(herbsBefore + herbIncome - herbUpkeep, sect.Herbs,
                "灵石供养不足时不得扣普通修炼或重点培养灵材。");
        else
            Assert.AreEqual(Math.Max(0, stonesBefore + stoneIncome - stoneUpkeep), sect.Stones,
                "灵材供养不足时仍只结算正常灵石供养。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void OrdinaryBreakthroughCannotSpendTheMatureFocusedCultivatorReserve()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var priority = members.Single(p => p.Id == sect.LeaderId);
        var ordinary = members.First(p => p.Id != priority.Id);
        priority.Realm = 4;
        priority.InnateTrait = 4;
        priority.Focused = true;
        priority.Progress = 120;
        priority.RealmMonths = WorldFactory.MinimumRealmMonths[priority.Realm];
        priority.AgeMonths = 500;
        ordinary.Realm = 0;
        ordinary.InnateTrait = 0;
        ordinary.Focused = false;
        ordinary.Progress = 120;
        ordinary.RealmMonths = WorldFactory.MinimumRealmMonths[ordinary.Realm];
        ordinary.AgeMonths = 100;
        sect.Policy = "balanced";
        sect.Target = "";

        var reserve = GameEngine.ReservedStones(world, sect);
        var ordinaryCost = GameEngine.BreakthroughCost(ordinary, prepared: false);
        var priorityCost = GameEngine.BreakthroughCost(priority, prepared: false);
        Assert.IsGreaterThan(ordinaryCost.Stones, priorityCost.Stones);
        var stonesAvailable = reserve + ordinaryCost.Stones;
        var herbsAvailable = GameEngine.ReservedHerbs(world, sect) + ordinaryCost.Herbs;
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);
        sect.Stones = stonesAvailable - stoneIncome + stoneUpkeep;
        sect.Herbs = herbsAvailable - herbIncome + herbUpkeep;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(4, priority.Realm, "高阶重点候选尚未积足突破资源，不应被普通门人抢占预算。");
        Assert.AreEqual(0, ordinary.Realm);
        Assert.AreEqual(stonesAvailable, sect.Stones);
        Assert.AreEqual(herbsAvailable, sect.Herbs);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void PriorityBreakthroughAt110ProtectsItsHerbsFromDailyCultivation()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var priority = members.Single(p => p.Id == sect.LeaderId);
        foreach (var person in members) person.Progress = 120;
        priority.Realm = 4;
        priority.InnateTrait = 4;
        priority.Focused = true;
        priority.Progress = 110;
        priority.RealmMonths = WorldFactory.MinimumRealmMonths[priority.Realm];
        priority.AgeMonths = 500;
        priority.Injury = 0;

        var reserveHerbs = GameEngine.ReservedHerbs(world, sect);
        var reserveStones = GameEngine.ReservedStones(world, sect);
        var breakthroughCost = GameEngine.BreakthroughCost(priority, prepared: false);
        var stoneIncome = GameEngine.Income(world, sect);
        var stoneUpkeep = GameEngine.Upkeep(world, sect);
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);
        var stonesAfterUpkeep = reserveStones + breakthroughCost.Stones;
        var herbsAfterUpkeep = reserveHerbs + breakthroughCost.Herbs;
        sect.Stones = stonesAfterUpkeep - stoneIncome + stoneUpkeep;
        sect.Herbs = herbsAfterUpkeep - herbIncome + herbUpkeep;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(10, priority.Progress, "日常修炼不得先花掉本月优先破境预留的灵材。");
        Assert.IsTrue(world.History.Any(e => e.Kind == "cultivation" && e.Text.Contains(priority.Name)),
            "修为重置或破境史记应证明本月实际尝试了突破。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AscensionWarningsQueueAll119MonthCandidatesBeforeAdvancing()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        var at119 = members.OrderBy(p => p.Id).Take(2).ToArray();
        var focused108 = members.First(p => p.Id != at119[0].Id && p.Id != at119[1].Id);
        foreach (var person in at119)
        {
            person.RealmMonths = 119;
            person.Focused = false;
            person.AscensionWarned = false;
            person.RetainInWorld = false;
        }
        focused108.RealmMonths = 108;
        focused108.Focused = true;
        focused108.AscensionWarned = false;
        focused108.RetainInWorld = false;
        var monthBefore = world.Month;
        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;

        void ChooseWarning(Cultivator expected, string option)
        {
            GameEngine.AdvanceMonth(world, QuietDecisions(world));
            Assert.IsNotNull(world.PendingEvent);
            var warning = world.PendingEvent!;
            Assert.AreEqual(monthBefore, world.Month, "飞升选择未全部处理前不得推进月结。");
            Assert.AreEqual(119, at119[0].RealmMonths);
            Assert.AreEqual(119, at119[1].RealmMonths);
            Assert.AreEqual(108, focused108.RealmMonths);
            Assert.AreEqual("ascension", warning.Type);
            Assert.AreEqual(expected.Id.ToString(), warning.Target);
            Assert.IsTrue(expected.AscensionWarned);
            Assert.AreEqual(stonesBefore, sect.Stones);
            Assert.AreEqual(herbsBefore, sect.Herbs);

            GameEngine.Command(world, new GameCommand("choose", Option: option));
            Assert.IsNull(world.PendingEvent);
            Assert.AreEqual(monthBefore, world.Month);
            Assert.AreEqual(stonesBefore, sect.Stones, "飞升议事选项不应立即收费。");
            Assert.AreEqual(herbsBefore, sect.Herbs, "飞升议事选项不应立即收费。");
        }

        ChooseWarning(at119[0], "ascend");
        ChooseWarning(at119[1], "retain");
        ChooseWarning(focused108, "retain");

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(monthBefore + 1, world.Month);
        Assert.IsFalse(at119[0].Alive);
        Assert.IsTrue(at119[0].Ascended);
        Assert.IsTrue(at119[1].Alive);
        Assert.IsTrue(at119[1].RetainInWorld);
        Assert.AreEqual(120, at119[0].RealmMonths);
        Assert.AreEqual(120, at119[1].RealmMonths);
        Assert.AreEqual(109, focused108.RealmMonths);
        Assert.IsNull(world.PendingEvent);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void RetentionCommandRejectsNonMahayanaAndInvalidOptionWithoutSpending()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var person = GameEngine.Members(world, sect).Single(p => p.Id == sect.LeaderId);
        sect.Stones = 12_345;
        sect.Herbs = 6_789;
        person.Realm = WorldFactory.MaximumRealm - 1;
        var stones = sect.Stones;
        var herbs = sect.Herbs;

        Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("retention", person.Id.ToString(), "on")));
        Assert.AreEqual(stones, sect.Stones);
        Assert.AreEqual(herbs, sect.Herbs);
        Assert.IsFalse(person.RetainInWorld);

        person.Realm = WorldFactory.MaximumRealm;
        Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("retention", person.Id.ToString(), "invalid")));
        Assert.AreEqual(stones, sect.Stones);
        Assert.AreEqual(herbs, sect.Herbs);
        Assert.IsFalse(person.RetainInWorld);
    }

    [TestMethod]
    public void InnateRealmLimitsBlockMortalBreakthroughButPermitRareTraitAdvancement()
    {
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 6, 7 }, WorldFactory.InnateRealmLimits);
        Assert.HasCount(WorldFactory.MaximumRealm + 1, WorldFactory.Lifespans, "境界上限应与寿元表一一对应。");
        for (var trait = 0; trait < WorldFactory.InnateRealmLimits.Length; trait++)
        {
            var cultivator = new Cultivator { InnateTrait = trait, Realm = WorldFactory.InnateRealmLimits[trait] - 1 };
            Assert.IsTrue(GameEngine.CanAdvanceRealm(cultivator), $"特性 {trait} 应可晋入上限境界。");
            cultivator.Realm++;
            Assert.IsFalse(GameEngine.CanAdvanceRealm(cultivator), $"特性 {trait} 不得突破自身境界上限。");
        }

        var world = CreateWorld();
        var sect = world.Player;
        var person = GameEngine.Members(world, sect).Single(p => p.Id == sect.LeaderId);
        sect.Stones = 100_000;
        sect.Herbs = 100_000;
        sect.Doctrine = 8;
        person.Realm = 2;
        person.InnateTrait = 0;
        person.Potential = 100;
        person.Progress = 120;
        person.RealmMonths = WorldFactory.MinimumRealmMonths[person.Realm];
        person.Injury = 0;
        var stonesBefore = sect.Stones;
        var herbsBefore = sect.Herbs;

        Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("breakthrough", person.Id.ToString())));
        Assert.AreEqual(stonesBefore, sect.Stones);
        Assert.AreEqual(herbsBefore, sect.Herbs);
        Assert.AreEqual(2, person.Realm, "凡体到金丹后不得跨入元婴。");

        person.InnateTrait = 4;
        world.RandomState = 1;
        var cost = GameEngine.BreakthroughCost(person, prepared: true);
        GameEngine.Command(world, new GameCommand("breakthrough", person.Id.ToString()));

        Assert.AreEqual(3, person.Realm, "先天道体可从金丹实际晋入元婴。");
        Assert.AreEqual(stonesBefore - cost.Stones, sect.Stones);
        Assert.AreEqual(herbsBefore - cost.Herbs, sect.Herbs);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void FullProgressAtMaximumRealmStillPaysMonthlyHerbUpkeep()
    {
        var world = CreateWorld();
        var sect = world.Player;
        var members = ConfigureMahayanaRoster(world, sect);
        foreach (var person in members)
        {
            person.Progress = 120;
            person.RealmMonths = 0;
        }
        sect.Herbs = 100_000;
        var herbsBefore = sect.Herbs;
        var herbIncome = GameEngine.HerbIncome(world, sect);
        var herbUpkeep = GameEngine.HerbUpkeep(world, sect);
        Assert.AreEqual(members.Count * 150, herbUpkeep);

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(herbsBefore + herbIncome - herbUpkeep, sect.Herbs);
        Assert.IsTrue(members.All(p => p.Progress == 120));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void RareInnateTraitDoublesActualBattleDamage()
    {
        var world = CreateWorld();
        var attackerSect = world.Player;
        var defenderSect = world.Sects.Single(s => s.Id == "chixia");
        var attacker = GameEngine.Members(world, attackerSect)[0];
        var defender = GameEngine.Members(world, defenderSect)[0];
        foreach (var person in GameEngine.Members(world, attackerSect).Where(p => p.Id != attacker.Id)) person.Alive = false;
        foreach (var person in GameEngine.Members(world, defenderSect).Where(p => p.Id != defender.Id)) person.Alive = false;
        attackerSect.LeaderId = attacker.Id;
        defenderSect.LeaderId = defender.Id;
        attacker.Realm = 3;
        attacker.InnateTrait = 0;
        attacker.Potential = 55;
        attacker.Injury = 0;
        defender.Realm = 0;
        defender.InnateTrait = 0;
        defender.Potential = 55;
        defender.Injury = 95;
        var regionId = world.Regions.First(r => r.Owner == defenderSect.Id).Id;
        var lowTrait = world.Copy();
        var rareTrait = world.Copy();
        lowTrait.RandomState = rareTrait.RandomState = 1;
        rareTrait.People.Single(p => p.Id == attacker.Id).InnateTrait = 4;
        var resolve = typeof(GameEngine).GetMethod("ResolveCultivatorBattle", BindingFlags.Static | BindingFlags.NonPublic)!;

        static BattleReport Fight(GameState state, int attackerId, string targetId, MethodInfo resolver) =>
            (BattleReport)resolver.Invoke(null,
                [state, state.Player, state.Regions.Single(r => r.Id == targetId), "balanced", 1d, 1d, new[] { attackerId }])!;

        var baseline = Fight(lowTrait, attacker.Id, regionId, resolve);
        var empowered = Fight(rareTrait, attacker.Id, regionId, resolve);
        var baselineStrike = baseline.Exchanges.Single(e => e.AttackerId == attacker.Id && e.DefenderId == defender.Id);
        var empoweredStrike = empowered.Exchanges.Single(e => e.AttackerId == attacker.Id && e.DefenderId == defender.Id);

        Assert.IsTrue(baseline.AttackerWon && empowered.AttackerWon);
        Assert.AreEqual(1, baselineStrike.Hits);
        Assert.AreEqual(1, empoweredStrike.Hits);
        Assert.AreEqual(baselineStrike.Damage * 2, empoweredStrike.Damage, 0.000001);
    }

    private static GameState CreateWorld(int seed = 7001)
    {
        var world = WorldFactory.Create("qingyun", seed, "rules", "normal", compact: true);
        world.AutoStewardship = false;
        world.NextEventMonth = 10_000;
        GameEngine.Command(world, new GameCommand("choose", Option: "prosperity"));
        return world;
    }

    private static List<Cultivator> ConfigureMahayanaRoster(GameState world, Sect sect)
    {
        var members = GameEngine.Members(world, sect);
        foreach (var person in members)
        {
            person.Realm = WorldFactory.MaximumRealm;
            person.InnateTrait = 4;
            person.AgeMonths = 1_000;
            person.RealmMonths = 0;
            person.Progress = 0;
            person.Injury = 0;
            person.Focused = false;
            person.AscensionWarned = false;
            person.NextJourneyMonth = 10_000;
            person.RetainInWorld = false;
            person.Ascended = false;
        }
        sect.Stones = 1_000_000;
        sect.Herbs = 1_000_000;
        return members;
    }

    private static List<AgentDecision> QuietDecisions(GameState world) => world.Sects
        .Where(s => s.Id != world.PlayerId && !s.Eliminated)
        .Select(s => new AgentDecision
        {
            SectId = s.Id,
            Policy = "balanced",
            Target = "",
            Reason = "cultivation balance regression"
        }).ToList();
}
