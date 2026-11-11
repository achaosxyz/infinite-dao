using System.Text.RegularExpressions;
using System.Text.Json;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class CoreGameTests
{
    public TestContext TestContext { get; set; } = null!;

    private static GameState CreateWorld(int seed = 12345)
    {
        var world = WorldFactory.Create("qingyun", seed, "rules", "normal", compact: true);
        world.AutoStewardship = false;
        return world;
    }

    private static GameState CreateStandardWorld(int seed = 12345) =>
        WorldFactory.Create("qingyun", seed, "rules", "normal");

    private static List<AgentDecision> QuietDecisions(GameState world) => world.Sects
        .Where(sect => sect.Id != world.PlayerId && !sect.Eliminated)
        .Select(sect => new AgentDecision
        {
            SectId = sect.Id,
            Policy = "balanced",
            Target = "",
            Reason = "测试中保持诸宗边境安静。"
        }).ToList();

    private static int CultivationHerbUpkeep(GameState world, Sect sect) => GameEngine.Members(world, sect)
        .Where(person => person.Realm < 5 && person.Progress < 120)
        .Sum(person => 1 + person.Realm + (person.Focused ? 3 + person.Realm * 2 : 0));

    private static SectAgentPlan ValidAgentPlan(string target = "") => new()
    {
        Policy = "balanced",
        Target = target,
        Goal = "维持边境并积蓄军需。",
        Reason = "边境局势尚稳，暂以均衡发展为先。",
        Stages = ["整顿领地", "观察边境变化"],
        ReconsiderWhen = "目标被占领或资源形势显著变化时复盘。",
        ReviewAfterMonths = 60
    };

    private static void FinishInauguration(GameState world) =>
        GameEngine.Command(world, new GameCommand("choose", Option: "prosperity"));

    [TestMethod]
    public void CreationSupportsAllTenSectsAndEveryPolicy()
    {
        var templates = WorldFactory.Templates();
        Assert.HasCount(10, templates);
        Assert.AreEqual(10, templates.Select(sect => sect.Id).Distinct().Count());
        Assert.AreEqual(5, WorldFactory.Policies.Distinct().Count());

        foreach (var template in templates)
        {
            var world = WorldFactory.Create(template.Id, 17, "rules", "normal");
            Assert.AreEqual(template.Id, world.Player.Id);
            Assert.HasCount(10, world.Sects);
            Assert.HasCount(192, world.Regions);
            Assert.IsTrue(world.AutoStewardship, "默认大世界应开启自动执事");
            Assert.HasCount(16, GameEngine.Members(world, world.Player));
            Assert.HasCount(5, GameEngine.Lands(world, world.Player));
            GameEngine.Validate(world);
        }

        var strategyWorld = CreateWorld();
        FinishInauguration(strategyWorld);
        foreach (var policy in WorldFactory.Policies)
        {
            GameEngine.Command(strategyWorld, new GameCommand("policy", Option: policy));
            Assert.AreEqual(policy, strategyWorld.Player.Policy);
        }

        var proposals = strategyWorld.Sects.Where(sect => sect.Id != strategyWorld.PlayerId)
            .OrderBy(sect => sect.Id)
            .Select((sect, index) => new AgentDecision
            {
                SectId = sect.Id,
                Policy = WorldFactory.Policies[index % WorldFactory.Policies.Length],
                Reason = "按宗门策略推进。"
            }).ToList();
        GameEngine.ApplyDecisions(strategyWorld, proposals);
        foreach (var proposal in proposals)
            Assert.AreEqual(proposal.Policy, strategyWorld.Sects.Single(sect => sect.Id == proposal.SectId).Policy);

        var invalid = Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(strategyWorld, new GameCommand("policy", Option: "invalid")));
        StringAssert.Contains(invalid.Message, "策略");
    }

    [TestMethod]
    public void CopyDeepClonesSectAgentMemoryPlanConversationArchiveAndStages()
    {
        var world = CreateWorld(51);
        world.Month = 10;
        var rival = world.Sects.First(sect => sect.Id != world.PlayerId);
        world.AgentMemories.Add(new SectAgentMemory
        {
            SectId = rival.Id,
            Plan = ValidAgentPlan(),
            Summary = "边境和平，持续经营。",
            Conversation = [new AgentConversationEntry { Role = "user", Text = "查看近月变化。", Month = 8 }],
            Archive = [new AgentConversationEntry { Role = "assistant", Text = "旧计划已归档。", Month = 4 }],
            LastReviewMonth = 5,
            NextReviewMonth = 20,
            LastObservedEventId = 3,
            LastSituation = "边境平稳",
            TargetOwner = "",
            LastTrigger = "review"
        });

        var copy = world.Copy();
        var copiedMemory = copy.AgentMemories.Single();
        copiedMemory.Plan!.Policy = "cultivation";
        copiedMemory.Plan.Stages[0] = "修改后的计划阶段";
        copiedMemory.Conversation[0].Text = "修改后的对话";
        copiedMemory.Conversation.Add(new AgentConversationEntry { Role = "assistant", Text = "新对话", Month = 10 });
        copiedMemory.Archive[0].Text = "修改后的归档";
        copiedMemory.Summary = "修改后的摘要";

        var originalMemory = world.AgentMemories.Single();
        Assert.AreEqual("balanced", originalMemory.Plan!.Policy);
        Assert.AreEqual("整顿领地", originalMemory.Plan.Stages[0]);
        Assert.AreEqual("查看近月变化。", originalMemory.Conversation[0].Text);
        Assert.HasCount(1, originalMemory.Conversation);
        Assert.AreEqual("旧计划已归档。", originalMemory.Archive[0].Text);
        Assert.AreEqual("边境和平，持续经营。", originalMemory.Summary);
        GameEngine.Validate(world);
        GameEngine.Validate(copy);
    }

    [TestMethod]
    public void AgentPlanValidationRejectsInvalidOwnerPolicyTargetGoalStagesAndReviewHorizon()
    {
        var world = CreateStandardWorld(52);
        var rival = world.Sects.First(sect => sect.Id != world.PlayerId);
        GameEngine.ValidateAgentPlan(world, rival, ValidAgentPlan());

        void Reject(Sect sect, SectAgentPlan plan) =>
            Assert.Throws<GameRuleException>(() => GameEngine.ValidateAgentPlan(world, sect, plan));

        Reject(world.Player, ValidAgentPlan());

        var invalidPolicy = ValidAgentPlan();
        invalidPolicy.Policy = "not-a-policy";
        Reject(rival, invalidPolicy);

        var occupiedTarget = ValidAgentPlan(rival.Capital);
        Reject(rival, occupiedTarget);

        var unreachableRegion = world.Regions.First(region => region.Owner != rival.Id && !GameEngine.Border(world, rival, region));
        var unreachableTarget = ValidAgentPlan(unreachableRegion.Id);
        Reject(rival, unreachableTarget);

        var emptyGoal = ValidAgentPlan();
        emptyGoal.Goal = "";
        Reject(rival, emptyGoal);

        var emptyStages = ValidAgentPlan();
        emptyStages.Stages = [];
        Reject(rival, emptyStages);

        var tooSoon = ValidAgentPlan();
        tooSoon.ReviewAfterMonths = 11;
        Reject(rival, tooSoon);

        var tooLate = ValidAgentPlan();
        tooLate.ReviewAfterMonths = 1201;
        Reject(rival, tooLate);
    }

    [TestMethod]
    public void StoredAgentPlanMayBecomeStaleWithoutInvalidatingSave()
    {
        var world = CreateStandardWorld(53);
        var rival = world.Sects.First(sect => sect.Id != world.PlayerId);
        var target = world.Regions.First(region => region.Owner == "" && GameEngine.Border(world, rival, region));
        var plan = ValidAgentPlan(target.Id);
        GameEngine.ValidateAgentPlan(world, rival, plan);
        world.AgentMemories.Add(new SectAgentMemory { SectId = rival.Id, Plan = plan });

        target.Owner = rival.Id;

        Assert.Throws<GameRuleException>(() => GameEngine.ValidateAgentPlan(world, rival, plan),
            "下次唤醒时不得继续提交已占领目标");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void ReapplyingSameLlmMonthPlanDoesNotDuplicateDecisionHistory()
    {
        var world = CreateWorld(54);
        world.Month = 40;
        var decisions = world.Sects.Where(sect => sect.Id != world.PlayerId)
            .Select(sect => new AgentDecision
            {
                Month = 31,
                SectId = sect.Id,
                Policy = "balanced",
                Reason = "复用既定月度战略计划。",
                Source = "llm"
            }).ToList();

        GameEngine.ApplyDecisions(world, decisions);
        Assert.HasCount(decisions.Count, world.Decisions);
        Assert.IsTrue(world.Decisions.All(decision => decision.Month == 31));

        GameEngine.ApplyDecisions(world, decisions);

        Assert.HasCount(decisions.Count, world.Decisions, "同一宗门同月复用既定计划不得重复写入决策历史");
        Assert.IsTrue(world.Decisions.All(decision => decision.Source == "llm" && decision.Month == 31));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void StandardWorldHasAConnectedBidirectionalMapAndSeparatedSectHoldings()
    {
        var world = CreateStandardWorld(37);
        Assert.IsTrue(world.AutoStewardship);
        var regions = world.Regions.ToDictionary(region => region.Id);
        Assert.AreEqual(16, world.MapColumns);
        Assert.AreEqual(12, world.MapRows);
        Assert.HasCount(192, world.Regions);

        foreach (var region in world.Regions)
        foreach (var neighborId in region.Neighbors)
            Assert.Contains(region.Id, regions[neighborId].Neighbors, $"邻接必须双向：{region.Id} ↔ {neighborId}");

        var visited = new HashSet<string>();
        var frontier = new Queue<string>();
        frontier.Enqueue(world.Regions[0].Id);
        while (frontier.TryDequeue(out var current))
        {
            if (!visited.Add(current)) continue;
            foreach (var neighbor in regions[current].Neighbors) frontier.Enqueue(neighbor);
        }
        Assert.HasCount(world.Regions.Count, visited, "整张地图应连通");

        var holdings = world.Sects.Select(sect => GameEngine.Lands(world, sect)
            .Select(region => region.Id).ToHashSet()).ToArray();
        foreach (var holding in holdings) Assert.HasCount(5, holding, "每宗开局应占五处地域");
        for (var i = 0; i < holdings.Length; i++)
        for (var j = i + 1; j < holdings.Length; j++)
        {
            Assert.IsFalse(holdings[i].Overlaps(holdings[j]), "不同宗门的开局领地不得重叠");
            Assert.IsFalse(holdings[i].Any(id => regions[id].Neighbors.Any(holdings[j].Contains)), "开局宗门领地应彼此分隔");
        }
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void CampaignMinimumDurationScalesWithTerritoryFortAndHazard()
    {
        var world = CreateStandardWorld(39);
        var target = world.Regions.Single(region => region.Id == "r07");
        target.Fort = 3;
        target.Owner = "";
        target.Terrain = "平原";
        Assert.AreEqual(12 + target.Fort * 2, GameEngine.MinimumBattleMonths(world, target));
        target.Terrain = "关隘";
        Assert.AreEqual(12 + target.Fort * 2 + 6, GameEngine.MinimumBattleMonths(world, target));

        target.Owner = "chixia";
        target.Terrain = "平原";
        Assert.AreEqual(36 + target.Fort * 9, GameEngine.MinimumBattleMonths(world, target));
        target.Terrain = "雪原";
        Assert.AreEqual(36 + target.Fort * 9 + 6, GameEngine.MinimumBattleMonths(world, target));
    }

    [TestMethod]
    public void SimilarAggregatePowerStillRecordsRealmSuppressionInGoldenCoreVersusNascentSoulBattle()
    {
        var world = CreateWorld(42);
        FinishInauguration(world);
        world.NextEventMonth = 10000;
        var player = world.Player;
        var rival = world.Sects.Single(sect => sect.Id == "chixia");
        var target = world.Regions.Single(region => region.Id == "r03");
        world.Regions.Single(region => region.Id == "r07").Owner = player.Id;

        var attackers = GameEngine.Members(world, player).OrderBy(person => person.Id).ToList();
        foreach (var person in attackers)
        {
            person.Realm = 0; person.Potential = 55; person.Injury = 0; person.Progress = 0; person.RealmMonths = 1000;
        }
        foreach (var person in attackers.Take(11)) person.Realm = 2;
        var defenders = GameEngine.Members(world, rival).OrderBy(person => person.Id).ToList();
        foreach (var person in defenders)
        {
            person.Realm = 0; person.Potential = 55; person.Injury = 0; person.Progress = 0; person.RealmMonths = 1000;
        }
        foreach (var person in defenders.Take(4)) person.Realm = 3;
        player.Stones = 100000; player.Herbs = 100000;
        rival.Stones = 0;

        var relativePowerGap = Math.Abs(GameEngine.Power(world, player) - GameEngine.Power(world, rival))
            / (double)Math.Max(GameEngine.Power(world, player), GameEngine.Power(world, rival));
        Assert.IsLessThan(0.05, relativePowerGap, "双方聚合战力保持近似对称");
        Assert.AreEqual(0.12, GameEngine.RealmDamageMultiplier(2, 3), 1e-12);
        Assert.AreEqual(0.0144, GameEngine.RealmDamageMultiplier(2, 4), 1e-12);

        GameEngine.Command(world, new GameCommand("attack", target.Id));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));
        var campaign = world.Campaigns.Single(item => item.Attacker == player.Id);
        campaign.MonthsRemaining = 1;
        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        var report = world.BattleReports.Single();
        Assert.IsTrue(report.Exchanges.Any(exchange => exchange.AttackerRealm == 2 && exchange.DefenderRealm == 3
            && Math.Abs(exchange.RealmMultiplier - 0.12) < 1e-12), "金丹修士攻击元婴修士应留下受压制的交锋记录");
        Assert.IsTrue(report.Exchanges.Any(exchange => exchange.AttackerRealm == 3 && exchange.DefenderRealm == 2
            && Math.Abs(exchange.RealmMultiplier - 1.35) < 1e-12), "元婴修士反击金丹修士应留下跨境界交锋记录");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void GuardiansMustBeHigherRealmUniqueAndNotAwayOnAnotherDuty()
    {
        var world = CreateWorld(44);
        FinishInauguration(world);
        world.NextEventMonth = 10000;
        var player = world.Player;
        var members = GameEngine.Members(world, player);
        var guardian = members.Single(person => person.Id == player.LeaderId);
        var disciples = members.Where(person => person.Role != "掌门" && person.Realm == 0).OrderBy(person => person.Id).ToList();
        var first = disciples[0];
        var second = disciples[1];

        var sameRealm = Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("guardian", first.Id.ToString(), second.Id.ToString())));
        StringAssert.Contains(sameRealm.Message, "境界更高");

        GameEngine.Command(world, new GameCommand("guardian", first.Id.ToString(), guardian.Id.ToString()));
        Assert.AreEqual(guardian.Id, first.GuardianId);
        var duplicateGuard = Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("guardian", second.Id.ToString(), guardian.Id.ToString())));
        StringAssert.Contains(duplicateGuard.Message, "只能保护一名");
        GameEngine.Command(world, new GameCommand("guardian", first.Id.ToString()));

        var target = world.Regions.Single(region => region.Id == "r07");
        GameEngine.Command(world, new GameCommand("attack", target.Id));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));
        var campaign = world.Campaigns.Single(item => item.Attacker == player.Id);
        Assert.Contains(guardian.Id, campaign.Participants);
        Assert.IsTrue(GameEngine.IsAway(world, guardian.Id));
        var homeDisciple = disciples.First(person => !campaign.Participants.Contains(person.Id));
        var doubleDuty = Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("guardian", homeDisciple.Id.ToString(), guardian.Id.ToString())));
        StringAssert.Contains(doubleDuty.Message, "当前未出征或历练");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void GuardingExpeditionReducesRiskAndAutomaticJourneysResolveOverTheirRecordedDuration()
    {
        var unguarded = CreateWorld(46);
        FinishInauguration(unguarded);
        var guarded = unguarded.Copy();
        var discipleId = GameEngine.Members(unguarded, unguarded.Player).First(person => person.Role != "掌门" && person.Realm == 0).Id;
        var guardianId = unguarded.Player.LeaderId;
        var dangerRegion = unguarded.Regions.Single(region => region.Id == "r02");

        void PrepareJourney(GameState state, int? escort)
        {
            state.AutoStewardship = true;
            state.NextEventMonth = 10000;
            foreach (var person in GameEngine.Members(state, state.Player))
            {
                person.Progress = 0;
                person.NextJourneyMonth = 10000;
            }
            var disciple = state.People.Single(person => person.Id == discipleId);
            disciple.GuardianId = escort;
            var origin = GameEngine.Log(state, "journey", "测试历练出发", "受控风险对照。", [state.PlayerId]);
            state.Expeditions.Add(new Expedition
            {
                Id = state.NewId(), DiscipleId = discipleId, GuardianId = escort, SectId = state.PlayerId,
                RegionId = dangerRegion.Id, MonthsRemaining = 1, OriginEvent = origin
            });
            state.RandomState = 960;
        }

        PrepareJourney(unguarded, null);
        PrepareJourney(guarded, guardianId);
        GameEngine.AdvanceMonth(unguarded, QuietDecisions(unguarded));
        GameEngine.AdvanceMonth(guarded, QuietDecisions(guarded));

        Assert.IsFalse(unguarded.People.Single(person => person.Id == discipleId).Alive,
            "固定随机数应使无护道人在危险地域遇难");
        Assert.IsTrue(guarded.People.Single(person => person.Id == discipleId).Alive,
            "护道人同行应显著降低同一趟历练风险");
        Assert.IsFalse(GameEngine.IsAway(guarded, discipleId));
        Assert.IsFalse(GameEngine.IsAway(guarded, guardianId));
        GameEngine.Validate(unguarded);
        GameEngine.Validate(guarded);
    }

    [TestMethod]
    public void AutomaticStewardshipCultivatesAndCompletesJourneysWithoutManualRecruitment()
    {
        var world = CreateStandardWorld(48);
        Assert.IsTrue(world.AutoStewardship);
        FinishInauguration(world);
        world.NextEventMonth = 10000;
        world.Player.Stones = 100000;
        world.Player.Herbs = 100000;
        var members = GameEngine.Members(world, world.Player);
        var focused = members.First(person => person.Role == "长老");
        var ordinary = members.First(person => person.Role == "长老" && person.Id != focused.Id);
        foreach (var person in members)
        {
            person.Progress = 0;
            person.Realm = 0;
            person.RealmMonths = 0;
            person.Potential = 80;
            person.Injury = 0;
            person.NextJourneyMonth = person.Id == focused.Id || person.Id == ordinary.Id || person.Role == "掌门" ? 10000 : 1;
        }
        world.Player.Policy = "cultivation";
        GameEngine.Command(world, new GameCommand("focus", focused.Id.ToString(), "on"));
        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsGreaterThan(ordinary.Progress, focused.Progress, "重点培养应带来可观察的自动修为增长");
        var initialJourneys = world.Expeditions.Where(expedition => expedition.SectId == world.PlayerId).ToList();
        Assert.IsGreaterThan(0, initialJourneys.Count, "执事应按期安排门人下山历练");
        var origins = initialJourneys.Select(expedition => expedition.OriginEvent).ToHashSet();
        var longestJourney = initialJourneys.Max(expedition => expedition.MonthsRemaining);

        for (var month = 0; month < longestJourney; month++)
            GameEngine.AdvanceMonth(world, QuietDecisions(world));

        foreach (var origin in origins)
            Assert.IsTrue(world.History.Any(entry => entry.Causes.Contains(origin) && entry.Kind is "journey" or "death"),
                $"历练 {origin} 应有归山或遇险结局");
        Assert.IsTrue(world.History.Any(entry => entry.Kind == "journey" && entry.Title.EndsWith("下山历练")),
            "自动执事应保留历练出发史");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AutomaticHerbPurchasesChargeFourStonesPerHerbOnlyForCultivators()
    {
        var world = CreateStandardWorld(49);
        FinishInauguration(world);
        world.Month = 3;
        world.NextEventMonth = 10000;
        var player = world.Player;
        player.Policy = "prosperity";
        player.Target = "";
        var members = GameEngine.Members(world, player);
        foreach (var person in members)
        {
            person.Realm = 5;
            person.Progress = 120;
            person.Focused = false;
            person.AgeMonths = 40 * 12;
            person.NextJourneyMonth = 10000;
        }
        var cultivator = members[0];
        cultivator.Realm = 0;
        cultivator.Progress = 0;

        var income = GameEngine.Income(world, player);
        var upkeep = GameEngine.Upkeep(world, player);
        var herbIncome = GameEngine.HerbIncome(world, player);
        player.Stones = 100000;
        player.Herbs = 220 - herbIncome;
        var startingHerbs = player.Herbs;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(225, player.Herbs, "坊市只补足一名真实修炼者的六份储备需求，扣除本月一份修炼丹材");
        Assert.AreEqual(24, 100000 + income - upkeep - player.Stones, "实际购材应按每份丹材四枚灵石结算");
        Assert.IsGreaterThanOrEqualTo(GameEngine.ReservedStones(world, player), player.Stones,
            "采办不得侵占灵石储备");
        Assert.IsGreaterThan(0, cultivator.Progress, "未满修为的门人仍应正常修炼");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AutomaticHerbPurchasesPreserveTheFullTargetCampaignReserve()
    {
        var world = CreateStandardWorld(50);
        FinishInauguration(world);
        world.Month = 3;
        world.NextEventMonth = 10000;
        var player = world.Player;
        var target = world.Regions.First(region => region.Owner != player.Id && GameEngine.Border(world, player, region));
        player.Policy = "expansion";
        player.Target = target.Id;
        foreach (var person in GameEngine.Members(world, player))
        {
            person.Realm = 5;
            person.Progress = 120;
            person.AgeMonths = 40 * 12;
            person.NextJourneyMonth = 10000;
        }

        var reserve = GameEngine.ReservedStones(world, player);
        var income = GameEngine.Income(world, player);
        var upkeep = GameEngine.Upkeep(world, player);
        var herbIncome = GameEngine.HerbIncome(world, player);
        player.Stones = reserve - income + upkeep;
        player.Herbs = 0;

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(reserve, player.Stones, "只达到远征全程军需储备时，执事不得用灵石购材");
        Assert.AreEqual(herbIncome, player.Herbs, "军需储备不足以购材时，不得凭空补充灵材");
        Assert.AreEqual(reserve, GameEngine.ReservedStones(world, player));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void CompactManualFixturesDisableAutomaticStewardship()
    {
        var world = CreateWorld(41);
        Assert.IsFalse(world.AutoStewardship, "人工夹具不得被自动执事改变状态");
    }

    [TestMethod]
    [DataRow("develop", 180, 0)]
    [DataRow("fortify", 160, 15)]
    [DataRow("recruit", 140, 0)]
    [DataRow("train", 220, 30)]
    [DataRow("research", 300, 60)]
    [DataRow("breakthrough", 100, 45)]
    [DataRow("heal", 50, 50)]
    [DataRow("scout", 70, 0)]
    [DataRow("gift", 120, 0)]
    [DataRow("truce", 180, 0)]
    public void CommandsDeductTheirDeclaredResources(string command, int stoneCost, int herbCost)
    {
        var world = CreateWorld();
        FinishInauguration(world);
        var player = world.Player;
        player.Stones = 5000;
        player.Herbs = 2000;
        if (command == "truce")
            GameEngine.ChangeRelation(player, world.Sects.Single(s => s.Id == "chixia"), 45);
        var beforeStones = player.Stones;
        var beforeHerbs = player.Herbs;
        var target = command is "gift" or "truce" ? "chixia" : player.Capital;
        if (command == "breakthrough")
        {
            player = world.Player;
            var candidate = GameEngine.Members(world, player)[0];
            candidate.Realm = 0;
            candidate.Progress = 80;
            candidate.Injury = 0;
            target = candidate.Id.ToString();
        }

        GameEngine.Command(world, new GameCommand(command, target));

        Assert.AreEqual(beforeStones - stoneCost, player.Stones, $"灵石扣减：{command}");
        Assert.AreEqual(beforeHerbs - herbCost, player.Herbs, $"灵材扣减：{command}");
        Assert.AreEqual(1, world.CommandsLeft);
    }

    [TestMethod]
    public void ExplorationDeductsItsCostBeforeApplyingRandomRewards()
    {
        var world = CreateWorld(41);
        FinishInauguration(world);
        var player = world.Player;
        player.Stones = 1000;
        player.Herbs = 500;
        var beforeStones = player.Stones;
        var beforeHerbs = player.Herbs;

        GameEngine.Command(world, new GameCommand("explore"));

        var result = world.History.Last();
        if (result.Title == "秘境有获")
        {
            var reward = int.Parse(Regex.Match(result.Delta, @"灵石 \+(\d+)").Groups[1].Value);
            Assert.AreEqual(beforeStones - 120 + reward, player.Stones);
            Assert.AreEqual(beforeHerbs + 20, player.Herbs);
        }
        else if (result.Title == "古物重见天日")
        {
            Assert.AreEqual("relic", result.Kind);
            Assert.AreEqual(beforeStones - 120, player.Stones);
            Assert.AreEqual(beforeHerbs - 10, player.Herbs);
        }
        else
        {
            Assert.AreEqual("险地归来", result.Title);
            Assert.AreEqual(beforeStones - 120, player.Stones);
            Assert.AreEqual(beforeHerbs + 40, player.Herbs);
        }
    }

    [TestMethod]
    public void CouncilChoiceChargesItsDeclaredCost()
    {
        var world = CreateWorld(43);
        FinishInauguration(world);
        world.Player.Stones = 1000;
        world.PendingEvent = new GameEvent
        {
            Id = world.NewId(), Type = "council0", Title = "纳徒之议",
            Choices = [new EventChoice { Id = "accept", Stones = 120 }]
        };
        var beforeStones = world.Player.Stones;
        var beforeMembers = GameEngine.Members(world, world.Player).Count;

        GameEngine.Command(world, new GameCommand("choose", Option: "accept"));

        Assert.AreEqual(beforeStones - 120, world.Player.Stones);
        Assert.HasCount(beforeMembers + 1, GameEngine.Members(world, world.Player));
        Assert.IsNull(world.PendingEvent);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AttackCreatesAMultiMonthCampaignWithSupplyFatigueAndCausalBattleRecord()
    {
        var world = CreateWorld(7);
        FinishInauguration(world);
        world.NextEventMonth = 1000;
        var player = world.Player;
        var rival = world.Sects.Single(s => s.Id == "chixia");
        var target = world.Regions.Single(r => r.Id == "r03");
        world.Regions.Single(r => r.Id == "r07").Owner = player.Id;
        player.Capital = "r07";
        foreach (var person in GameEngine.Members(world, player))
        {
            person.Realm = 5;
            person.Potential = 100;
            person.Progress = 0;
        }
        foreach (var person in GameEngine.Members(world, rival))
        {
            person.Realm = 0;
            person.Potential = 0;
            person.AgeMonths = 40 * 12;
        }
        world.RandomState = 12345;
        player.Stones = 50_000;
        player.Herbs = 50_000;

        GameEngine.Command(world, new GameCommand("train"));
        var afterTrainingStones = player.Stones;
        var afterTrainingHerbs = player.Herbs;
        GameEngine.Command(world, new GameCommand("attack", target.Id));
        Assert.AreEqual(afterTrainingStones, player.Stones, "发起议事时尚未扣除出征费用");
        Assert.AreEqual(afterTrainingHerbs, player.Herbs);

        GameEngine.Command(world, new GameCommand("choose", Option: "cancel"));
        Assert.AreEqual(afterTrainingStones, player.Stones, "取消出征不应扣费");
        Assert.AreEqual(afterTrainingHerbs, player.Herbs);

        GameEngine.Command(world, new GameCommand("attack", target.Id));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));

        var campaign = world.Campaigns.Single();
        Assert.AreEqual(world.Month, campaign.StartedMonth);
        Assert.IsGreaterThanOrEqualTo(GameEngine.MinimumBattleMonths(world, target), campaign.Duration);
        Assert.AreEqual(afterTrainingStones - 130, player.Stones);
        Assert.AreEqual(afterTrainingHerbs - 20, player.Herbs);
        Assert.AreEqual(rival.Id, target.Owner, "确认出征不能在同月直接夺城");
        var roster = GameEngine.Members(world, player);
        var deployed = campaign.Participants.ToHashSet();
        Assert.IsLessThan(roster.Count, deployed.Count, "远征应保留部分门人在山门守护");
        Assert.IsTrue(deployed.All(id => GameEngine.IsAway(world, id)), "出征门人应标记为外出");
        Assert.IsTrue(roster.Where(person => !deployed.Contains(person.Id)).All(person => !GameEngine.IsAway(world, person.Id)),
            "留守门人不得被算作远征队员");

        for (var month = 1; month < campaign.Duration; month++)
        {
            var expectedStones = player.Stones + GameEngine.Income(world, player) - GameEngine.Upkeep(world, player) - campaign.MonthlyStones;
            var herbUpkeep = CultivationHerbUpkeep(world, player);
            var expectedHerbs = player.Herbs + GameEngine.HerbIncome(world, player) - herbUpkeep - campaign.MonthlyHerbs;
            var remaining = campaign.MonthsRemaining;
            GameEngine.AdvanceMonth(world, QuietDecisions(world));
            Assert.AreEqual(expectedStones, player.Stones, "逐月扣除远征灵石补给");
            Assert.AreEqual(expectedHerbs, player.Herbs, "逐月扣除远征灵材补给");
            Assert.AreEqual(remaining - 1, campaign.MonthsRemaining);
            Assert.AreEqual(rival.Id, target.Owner, "战役结束前领土控制权不变");
            if (month == 1)
            {
                var duplicate = Assert.Throws<GameRuleException>(() => GameEngine.Command(world, new GameCommand("attack", target.Id)));
                StringAssert.Contains(duplicate.Message, "远征");
            }
        }

        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        Assert.AreEqual(player.Id, target.Owner, "优势军力应在远征完成后夺取地域");
        Assert.IsFalse(world.Campaigns.Any(item => item.Attacker == player.Id));
        var battle = world.History.Last(entry => entry.Kind == "war");
        CollectionAssert.Contains(battle.Causes.ToArray(), campaign.OriginEvent);
        var report = world.BattleReports.Single(item => item.EventId == battle.Id);
        Assert.AreEqual(player.Id, report.Attacker);
        Assert.AreEqual(rival.Id, report.Defender);
        Assert.IsGreaterThan(0, report.Rounds);
        Assert.IsGreaterThan(0, report.Exchanges.Count);
        Assert.IsTrue(report.Exchanges.All(exchange => exchange.Hits > 0 && exchange.Damage > 0));
        foreach (var exchange in report.Exchanges)
            Assert.AreEqual(GameEngine.RealmDamageMultiplier(exchange.AttackerRealm, exchange.DefenderRealm), exchange.RealmMultiplier, 1e-12);
        Assert.IsTrue(report.Exchanges.Any(exchange => exchange.AttackerRealm != exchange.DefenderRealm), "战报应记录跨境界交锋");
        var deadAttackers = deployed.Count(id => !world.People.Single(person => person.Id == id).Alive);
        var deadRivals = world.People.Count(person => person.SectId == rival.Id && !person.Alive);
        Assert.AreEqual(report.AttackerDeaths, deadAttackers, "进攻方战报伤亡应对应实际出征门人");
        Assert.AreEqual(report.DefenderDeaths, deadRivals, "守方战报伤亡应对应实际门人生死");
        Assert.IsGreaterThan(0, deadRivals, "本固定种子战役应复现至少一名人物战死");
        Assert.IsGreaterThan(0, player.WarExhaustion, "远征与交战应累积战疲");

        var exhaustionBeforeRest = player.WarExhaustion;
        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        Assert.IsLessThan(exhaustionBeforeRest, player.WarExhaustion, "脱离战线后应休养恢复战疲");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AdvancingMonthUpdatesEverySectAndMemberFromTheSameInitialState()
    {
        var world = CreateWorld(19);
        FinishInauguration(world);
        world.Month = 2;
        world.NextEventMonth = 1000;
        foreach (var sect in world.Sects)
        {
            sect.Policy = "balanced";
            sect.Target = "";
            if (sect.Id != world.PlayerId) sect.Stones = 0;
            foreach (var person in GameEngine.Members(world, sect)) person.Progress = 0;
        }

        var expected = world.Sects.ToDictionary(
            sect => sect.Id,
            sect => new
            {
                Stones = sect.Stones + GameEngine.Income(world, sect) - GameEngine.Upkeep(world, sect),
                Herbs = sect.Herbs + GameEngine.HerbIncome(world, sect) - CultivationHerbUpkeep(world, sect),
                Ages = GameEngine.Members(world, sect).ToDictionary(person => person.Id, person => person.AgeMonths)
            });

        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(3, world.Month);
        Assert.IsNull(world.PendingEvent);
        foreach (var sect in world.Sects)
        {
            Assert.AreEqual(expected[sect.Id].Stones, sect.Stones, $"灵石结算：{sect.Id}");
            Assert.AreEqual(expected[sect.Id].Herbs, sect.Herbs, $"灵材结算：{sect.Id}");
            foreach (var person in GameEngine.Members(world, sect))
                Assert.AreEqual(expected[sect.Id].Ages[person.Id] + 1, person.AgeMonths, $"人物月龄：{person.Id}");
        }
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void RivalConflictsClaimAContestedRegionOnlyOncePerWorldAdvance()
    {
        var world = CreateWorld(47);
        FinishInauguration(world);
        world.Month = 5;
        world.NextEventMonth = 1000;
        world.RandomState = 1;
        foreach (var rival in world.Sects.Where(sect => sect.Id != world.PlayerId))
        {
            rival.Stones = 0;
            rival.Target = "";
        }
        var contenders = new[] { "chixia", "tiangong" };
        foreach (var id in contenders)
        {
            var sect = world.Sects.Single(item => item.Id == id);
            sect.Policy = "expansion";
            sect.Target = "r07";
            sect.Stones = 1000;
            sect.Herbs = 200;
        }
        var contested = world.Regions.Single(region => region.Id == "r07");
        Assert.AreEqual("", contested.Owner);

        var decisions = world.Sects.Where(sect => sect.Id != world.PlayerId).Select(sect => new AgentDecision
        {
            SectId = sect.Id,
            Policy = contenders.Contains(sect.Id) ? "expansion" : "balanced",
            Target = contenders.Contains(sect.Id) ? contested.Id : "",
            Reason = "固定边境意图，用于验证争夺去重。"
        }).ToList();
        GameEngine.AdvanceMonth(world, decisions);

        Assert.AreEqual("", contested.Owner, "远征刚发起时仍未夺取地域");
        Assert.HasCount(1, world.Campaigns.Where(campaign => campaign.Target == contested.Id));
        Assert.AreEqual(1, world.History.Count(entry => entry.Kind == "campaign" && entry.Title.Contains(contested.Name)));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void CampaignWithSustainedSupplyShortageRetreatsWithoutConquest()
    {
        var world = CreateWorld(53);
        FinishInauguration(world);
        world.NextEventMonth = 1000;
        var player = world.Player;
        foreach (var region in world.Regions.Where(region => region.Owner == player.Id && region.Id != player.Capital))
            region.Owner = "";
        var target = world.Regions.Single(region => region.Id == "r07");
        target.Fort = 5;
        player.Stones = 130;
        player.Herbs = 20;
        foreach (var person in GameEngine.Members(world, player)) person.Progress = 0;

        GameEngine.Command(world, new GameCommand("attack", target.Id));
        GameEngine.Command(world, new GameCommand("choose", Option: "balanced"));
        var campaign = world.Campaigns.Single(item => item.Attacker == player.Id);
        var origin = campaign.OriginEvent;
        Assert.IsGreaterThanOrEqualTo(GameEngine.MinimumBattleMonths(world, target), campaign.Duration);
        Assert.AreEqual("", target.Owner);

        for (var month = 0; month < campaign.Duration && world.Campaigns.Any(item => item.Id == campaign.Id); month++)
            GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.IsFalse(world.Campaigns.Any(item => item.Attacker == player.Id), "补给连续不足应撤销远征");
        Assert.IsLessThan(0.5, campaign.Supply, "断供状态应低于战役维持阈值");
        Assert.AreEqual("", target.Owner, "断供撤军不得虚构战果");
        var retreat = world.History.Last(entry => entry.Title == "粮道断绝，撤军归山");
        CollectionAssert.Contains(retreat.Causes.ToArray(), origin);
        Assert.IsGreaterThan(0, player.WarExhaustion, "断供期间仍应承担战疲");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void BreakthroughRequiresRealmAgeForManualAndAutomaticAttempts()
    {
        var world = CreateWorld(59);
        FinishInauguration(world);
        world.NextEventMonth = 1000;
        var player = world.Player;
        player.Stones = 10000;
        player.Herbs = 10000;
        player.Doctrine = 8;
        player.Policy = "cultivation";
        CollectionAssert.AreEqual(new[] { 18, 60, 180, 600, 1800, 0 }, WorldFactory.MinimumRealmMonths);
        var candidate = GameEngine.Members(world, player)[0];
        candidate.Realm = 0;
        candidate.Injury = 0;
        candidate.Progress = 120;
        candidate.RealmMonths = 0;

        var tooYoung = Assert.Throws<GameRuleException>(() =>
            GameEngine.Command(world, new GameCommand("breakthrough", candidate.Id.ToString())));
        StringAssert.Contains(tooYoung.Message, "修龄");

        foreach (var person in GameEngine.Members(world, player)) person.Progress = 0;
        candidate.Progress = 109;
        GameEngine.AdvanceMonth(world, QuietDecisions(world));
        Assert.AreEqual(0, candidate.Realm, "修龄未满时自动修炼不得快速跨境");
        Assert.AreEqual(120, candidate.Progress);

        candidate.RealmMonths = WorldFactory.MinimumRealmMonths[candidate.Realm];
        var stones = player.Stones;
        var herbs = player.Herbs;
        GameEngine.Command(world, new GameCommand("breakthrough", candidate.Id.ToString()));
        Assert.AreEqual(stones - 100, player.Stones);
        Assert.AreEqual(herbs - 45, player.Herbs);
        Assert.AreEqual(10, candidate.Progress);
        Assert.AreEqual("cultivation", world.History.Last().Kind);
    }

    [TestMethod]
    public void MaxRealmAtMaxProgressDoesNotConsumeHerbsForFurtherCultivation()
    {
        var world = CreateWorld(60);
        FinishInauguration(world);
        world.NextEventMonth = 10000;
        var player = world.Player;
        foreach (var person in GameEngine.Members(world, player))
        {
            person.Realm = 5;
            person.Progress = 120;
            person.AgeMonths = 40 * 12;
        }
        GameEngine.Members(world, player)[0].Focused = true;
        player.Herbs = 0;
        var herbIncome = GameEngine.HerbIncome(world, player);

        Assert.AreEqual(0, CultivationHerbUpkeep(world, player));
        GameEngine.AdvanceMonth(world, QuietDecisions(world));

        Assert.AreEqual(herbIncome, player.Herbs, "返虚顶阶且修为已满者不应继续消耗日常修炼或重点培养丹材");
        Assert.IsTrue(GameEngine.Members(world, player).All(person => person.Progress == 120));
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void OnlyTheThreeStrongestOwnedRelicsContributeToSectPower()
    {
        var world = CreateWorld(61);
        FinishInauguration(world);
        var player = world.Player;
        foreach (var power in new[] { 45, 30, 100, 65, 10 })
            world.Relics.Add(new Relic { Id = world.NewId(), Owner = player.Id, Power = power });
        world.Relics.Add(new Relic { Id = world.NewId(), Owner = "chixia", Power = 10000 });

        var topIds = world.Relics.Where(relic => relic.Owner == player.Id).OrderByDescending(relic => relic.Power)
            .Take(3).Select(relic => relic.Id).ToHashSet();
        var topThreeOnly = world.Copy();
        topThreeOnly.Relics.RemoveAll(relic => relic.Owner == player.Id && !topIds.Contains(relic.Id));
        Assert.AreEqual(GameEngine.Power(topThreeOnly, topThreeOnly.Player), GameEngine.Power(world, player));

        world.Relics.Add(new Relic { Id = world.NewId(), Owner = player.Id, Power = 110 });
        topIds = world.Relics.Where(relic => relic.Owner == player.Id).OrderByDescending(relic => relic.Power)
            .Take(3).Select(relic => relic.Id).ToHashSet();
        topThreeOnly = world.Copy();
        topThreeOnly.Relics.RemoveAll(relic => relic.Owner == player.Id && !topIds.Contains(relic.Id));
        Assert.AreEqual(GameEngine.Power(topThreeOnly, topThreeOnly.Player), GameEngine.Power(world, player),
            "新增高阶法宝应替换最低的一件有效法宝");
    }

    [TestMethod]
    public void StandardWorldCannotEliminateAllFiveRivalsInItsFirstElevenMonths()
    {
        var world = CreateStandardWorld(67);
        FinishInauguration(world);
        for (var month = 0; month < 11 && world.Status == "playing"; month++)
        {
            ResolveEventsAndRebuild(world);
            if (world.Status != "playing") break;
            GameEngine.AdvanceMonth(world);
            GameEngine.Validate(world);
        }

        Assert.AreEqual(11, world.Month);
        Assert.IsTrue(world.Sects.Any(sect => sect.Id != world.PlayerId && !sect.Eliminated),
            "标准大地图开局十一月内不得消灭全部五个对手宗门");
        Assert.AreNotEqual("victory", world.Status);
    }

    [TestMethod]
    public void LosingLastGatePreservesSurvivorsAndAllowsRebuildingInTheSameWorld()
    {
        var world = CreateWorld(23);
        FinishInauguration(world);
        var player = world.Player;
        var rival = world.Sects.Single(s => s.Id == "chixia");
        foreach (var region in world.Regions.Where(region => region.Owner == player.Id)) region.Owner = "";
        var lastGate = world.Regions.Single(region => region.Id == "r07");
        lastGate.Owner = player.Id;
        player.Capital = lastGate.Id;
        var beforeIds = GameEngine.Members(world, player).Select(person => person.Id).ToHashSet();
        world.PendingEvent = new GameEvent
        {
            Id = world.NewId(), Type = "defense", Actor = rival.Id, Target = lastGate.Id,
            Choices = [new EventChoice { Id = "retreat" }]
        };

        GameEngine.Command(world, new GameCommand("choose", Option: "retreat"));

        Assert.IsTrue(player.Exiled);
        Assert.AreEqual(1, world.Rebirths);
        Assert.AreEqual(rival.Id, lastGate.Owner);
        Assert.IsEmpty(GameEngine.Lands(world, player));
        Assert.IsNotNull(world.PendingEvent);
        Assert.AreEqual("ember", world.PendingEvent.Type);
        var survivors = GameEngine.Members(world, player);
        Assert.IsTrue(survivors.Count is > 0 and <= 6);
        Assert.IsTrue(survivors.All(person => beforeIds.Contains(person.Id)));
        Assert.IsTrue(world.People.Any(person => beforeIds.Contains(person.Id) && person.SectId == "" && person.Role == "散修"));
        var exile = world.History.Last(entry => entry.Kind == "exile");
        Assert.HasCount(1, exile.Causes);
        Assert.AreEqual("war", world.History.Single(entry => entry.Id == exile.Causes[0]).Kind);

        GameEngine.Command(world, new GameCommand("choose", Option: "wander"));
        GameEngine.Command(world, new GameCommand("settle", "r06"));

        Assert.IsFalse(player.Exiled);
        Assert.AreEqual("r06", player.Capital);
        Assert.AreEqual(player.Id, world.Regions.Single(region => region.Id == "r06").Owner);
        Assert.HasCount(survivors.Count, GameEngine.Members(world, player));
        Assert.AreEqual(1, world.Rebirths);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void CopyIsDeepAndCausalValidationRejectsMissingHistoryReferences()
    {
        var world = CreateWorld(29);
        GameEngine.Validate(world);
        var copy = world.Copy();
        copy.Sects[0].Relations["chixia"] = 77;
        copy.Regions[0].Neighbors.Clear();
        copy.People[0].Biography = "copy only";
        copy.History[0].Causes.Add(123);
        copy.PendingEvent!.Choices[0].Label = "copy only";

        Assert.AreNotEqual(77, world.Sects[0].Relations["chixia"]);
        Assert.IsNotEmpty(world.Regions[0].Neighbors);
        Assert.AreNotEqual("copy only", world.People[0].Biography);
        Assert.IsEmpty(world.History[0].Causes);
        Assert.AreNotEqual("copy only", world.PendingEvent!.Choices[0].Label);
        GameEngine.Validate(world);

        world.History.Add(new Chronicle { Id = world.NewId(), Causes = [int.MaxValue] });
        var exception = Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
        StringAssert.Contains(exception.Message, "因果");
    }

    [TestMethod]
    public void EliminatingEveryRivalProducesTheUnifiedVictoryEnding()
    {
        var world = CreateWorld(31);
        FinishInauguration(world);
        foreach (var rival in world.Sects.Where(sect => sect.Id != world.PlayerId))
        {
            rival.Eliminated = true;
            rival.Capital = "";
            foreach (var region in world.Regions.Where(region => region.Owner == rival.Id)) region.Owner = "";
        }
        world.NextEventMonth = 1000;

        GameEngine.AdvanceMonth(world);

        Assert.AreEqual("victory", world.Status);
        Assert.IsNull(world.PendingEvent);
        Assert.AreEqual("victory", world.History.Last().Kind);
        StringAssert.Contains(world.History.Last().Title, "万宗归一");
    }

    [TestMethod]
    public void LongSeededWorldRunsRemainValidAndReproducible()
    {
        foreach (var seed in new[] { 3, 101, 2027 })
        {
            var first = RunWorld(seed, 300);
            var second = RunWorld(seed, 300);
            GameEngine.Validate(first);
            GameEngine.Validate(second);
            Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second), $"随机种子 {seed} 的世界演化应可复现");
        }
    }

    [TestMethod]
    [TestCategory("LongRunning")]
    public void StandardSeed8192PlayerCanExpandThroughSixThousandMonthsWithoutManualCombatCommands()
    {
        const int horizon = 6000;
        var world = CreateStandardWorld(8192);
        GameEngine.Command(world, new GameCommand("choose", Option: "expansion"));
        var initialPlayerLands = GameEngine.Lands(world, world.Player).Count;
        var maximumPlayerLands = initialPlayerLands;
        var focused = GameEngine.Members(world, world.Player).OrderByDescending(person => person.Realm)
            .ThenByDescending(person => person.Potential).First();
        GameEngine.Command(world, new GameCommand("focus", focused.Id.ToString(), "on"));

        var stalledCampaigns = new HashSet<int>();
        for (var month = 0; month < horizon && world.Status == "playing"; month++)
        {
            ResolveEventsAndRebuild(world);
            maximumPlayerLands = Math.Max(maximumPlayerLands, GameEngine.Lands(world, world.Player).Count);
            if (world.Status != "playing") break;
            if (world.Month > 0 && world.Month % 12 == 0)
            {
                var reserve = GameEngine.Upkeep(world, world.Player) * 6 + 300;
                var policy = world.Player.Stones < reserve || world.Player.Herbs < 180 ? "prosperity" : "expansion";
                if (world.Player.Policy != policy) GameEngine.Command(world, new GameCommand("policy", Option: policy));
            }

            var remainingBeforeAdvance = world.Campaigns.ToDictionary(campaign => campaign.Id, campaign => campaign.MonthsRemaining);
            GameEngine.AdvanceMonth(world);
            maximumPlayerLands = Math.Max(maximumPlayerLands, GameEngine.Lands(world, world.Player).Count);
            foreach (var campaign in world.Campaigns)
            {
                if (remainingBeforeAdvance.TryGetValue(campaign.Id, out var previous) && campaign.MonthsRemaining >= previous)
                    stalledCampaigns.Add(campaign.Id);
                if (world.Month - campaign.StartedMonth >= campaign.Duration) stalledCampaigns.Add(campaign.Id);
            }
        }
        ResolveEventsAndRebuild(world);

        var playerLands = GameEngine.Lands(world, world.Player).Count;
        var survivingRivals = world.Sects.Count(sect => sect.Id != world.PlayerId && !sect.Eliminated);
        var victories = world.BattleReports.Count(report => report.AttackerWon);
        var defeats = world.BattleReports.Count - victories;
        var journeyStarts = world.History.Where(entry => entry.Kind == "journey" && entry.Title.EndsWith("下山历练")).ToList();
        var journeyOutcomes = world.History.Count(entry => entry.Causes.Any(cause => journeyStarts.Any(start => start.Id == cause))
            && entry.Kind is "journey" or "death");
        var campaignStarts = world.History.Count(entry => entry.Kind == "campaign" && entry.Title.Contains("远征"));
        var playerCampaignStarts = world.History.Count(entry => entry.Kind == "campaign"
            && entry.Title.StartsWith(world.Player.Name + "远征", StringComparison.Ordinal));
        var supplyRetreats = world.History.Count(entry => entry.Title == "粮道断绝，撤军归山");
        var interruptedCampaigns = world.History.Count(entry => entry.Title == "远征中止");
        var playerBattles = world.BattleReports.Where(report => report.Attacker == world.PlayerId || report.Defender == world.PlayerId).ToList();
        var playerWins = playerBattles.Count(report => report.Attacker == world.PlayerId ? report.AttackerWon : !report.AttackerWon);
        var playerLosses = playerBattles.Count - playerWins;
        var landsBySect = string.Join(",", world.Sects.Select(sect => $"{sect.Id}:{GameEngine.Lands(world, sect).Count}"));
        var playerMembers = GameEngine.Members(world, world.Player);
        var realms = string.Join(",", playerMembers.GroupBy(person => person.Realm).OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}"));
        var target = world.Regions.SingleOrDefault(region => region.Id == world.Player.Target);
        var minimumMonths = target is null ? 0 : GameEngine.MinimumBattleMonths(world, target);
        var capital = world.Regions.FirstOrDefault(region => region.Id == world.Player.Capital);
        var distance = target is null || capital is null ? 0
            : Math.Abs(capital.Index % world.MapColumns - target.Index % world.MapColumns)
                + Math.Abs(capital.Index / world.MapColumns - target.Index / world.MapColumns);
        var plannedDuration = target is null ? 0 : minimumMonths + distance / 6;
        var plannedMonthlyStones = target is null ? 0 : 40 + target.Fort * 10 + distance * 2;
        var plannedMonthlyHerbs = target is null ? 0 : 8 + target.Fort * 2;
        var launchStones = target is null ? 0 : 130 + minimumMonths * (40 + target.Fort * 10) + GameEngine.Upkeep(world, world.Player) * 6;
        var launchHerbs = target is null ? 0 : 20 + minimumMonths * (8 + target.Fort * 2);
        var fullCampaignStones = target is null ? 0 : 130 + plannedDuration * plannedMonthlyStones + GameEngine.Upkeep(world, world.Player) * 6;
        var fullCampaignHerbs = target is null ? 0 : 20 + plannedDuration * plannedMonthlyHerbs;
        TestContext.WriteLine($"seed=8192 month={world.Month} status={world.Status}; playerLands={playerLands} maxPlayerLands={maximumPlayerLands}; survivingRivals={survivingRivals}; "
            + $"landsBySect=[{landsBySect}]; playerCampaignStarts={playerCampaignStarts} playerWins={playerWins} playerLosses={playerLosses}; "
            + $"campaignStarts={campaignStarts} campaignBattles={world.BattleReports.Count} wins={victories} losses={defeats}; "
            + $"supplyRetreats={supplyRetreats} interruptedCampaigns={interruptedCampaigns} activeCampaigns={world.Campaigns.Count}; "
            + $"stalledCampaigns={stalledCampaigns.Count}; journeysStarted={journeyStarts.Count} outcomes={journeyOutcomes}.");
        TestContext.WriteLine($"playerResources=stones:{world.Player.Stones}/{GameEngine.Income(world, world.Player)}/{GameEngine.Upkeep(world, world.Player)} "
            + $"herbs:{world.Player.Herbs}/{GameEngine.HerbIncome(world, world.Player)}; policy={world.Player.Policy}; target={target?.Id ?? "(none)"} "
            + $"owner={target?.Owner ?? "(none)"} border={target is not null && GameEngine.Border(world, world.Player, target)}; "
            + $"realmCounts=[{realms}] focused={playerMembers.Count(person => person.Focused)} available={playerMembers.Count(person => !GameEngine.IsAway(world, person.Id) && person.Injury < 60)}; "
            + $"targetFort={target?.Fort ?? 0} minimumMonths={minimumMonths} distance={distance} duration={plannedDuration} "
            + $"launchThreshold={launchStones}stones/{launchHerbs}herbs fullCampaignReserve={fullCampaignStones}stones/{fullCampaignHerbs}herbs.");

        Assert.IsTrue(world.Month >= horizon || world.Status != "playing", "模拟应达到 6000 月或进入终局");
        Assert.IsGreaterThan(0, playerLands, "玩家流亡后应能重建并保有领地");
        Assert.IsGreaterThan(0, playerCampaignStarts, "扩张战略应让玩家本人发起远征");
        Assert.IsGreaterThan(0, playerWins, "玩家应能赢得实际战役");
        Assert.IsGreaterThan(initialPlayerLands, maximumPlayerLands, "数百年策略经营应曾扩大玩家版图");
        Assert.IsGreaterThan(0, world.BattleReports.Count, "默认自动执事应推动战役产生实际胜负");
        Assert.IsGreaterThan(0, journeyStarts.Count, "默认自动执事应长期安排历练");
        Assert.IsGreaterThan(0, journeyOutcomes, "历练应在其记录时长后产生归山或遇险结局");
        Assert.IsEmpty(stalledCampaigns, "远征应逐月推进并在战役期限内结算，不得永久僵持");
        Assert.IsFalse(world.Player.Exiled, "测试终点应已完成流亡重建");
        GameEngine.Validate(world);
    }

    private static GameState RunWorld(int seed, int months)
    {
        var world = CreateStandardWorld(seed);
        for (var i = 0; i < months && world.Status == "playing"; i++)
        {
            ResolveEventsAndRebuild(world);
            if (world.Status != "playing") break;
            GameEngine.AdvanceMonth(world);
            GameEngine.Validate(world);
        }
        ResolveEventsAndRebuild(world);
        return world;
    }

    private static void ResolveEventsAndRebuild(GameState world)
    {
        var handled = 0;
        while (world.PendingEvent is not null)
        {
            Assert.IsLessThanOrEqualTo(8, ++handled, "处理议事时不应无限生成新议事");
            var current = world.PendingEvent;
            var choice = current.Type switch
            {
                "attack" => "cancel",
                "defense" => "retreat",
                "ember" => "wander",
                "inauguration" => "prosperity",
                _ => current.Choices.OrderBy(item => item.Stones + item.Herbs).First().Id
            };
            GameEngine.Command(world, new GameCommand("choose", Option: choice));
            GameEngine.Validate(world);
        }

        if (!world.Player.Exiled || world.Status != "playing") return;
        var free = world.Regions.FirstOrDefault(region => region.Owner == "");
        var target = free ?? world.Regions.First(region =>
        {
            var owner = world.Sects.SingleOrDefault(sect => sect.Id == region.Owner);
            return owner is not null && owner.Capital != region.Id;
        });
        GameEngine.Command(world, new GameCommand("settle", target.Id));
        GameEngine.Validate(world);
    }
}
