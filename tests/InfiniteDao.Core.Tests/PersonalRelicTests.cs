using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class PersonalRelicTests
{
    private static Relic AddRelic(GameState world, string templateId, Cultivator? holder = null)
    {
        var template = PersonalRelics.Find(templateId)!;
        var region = world.Player.Capital;
        var origin = GameEngine.Log(world, "journey", "归山", "归山", [world.PlayerId]);
        var relic = new Relic { Id = world.NewId(), Scope = "personal", TemplateId = templateId, Name = template.Name,
            FoundMonth = world.Month, Owner = world.PlayerId, HolderId = holder?.Id, RegionId = region };
        var change = GameEngine.Log(world, "relic_discovery", "得宝", "得宝", [world.PlayerId], causes: [origin]);
        relic.LastEventId = change; relic.OriginEvent = change;
        world.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = world.PlayerId, ToHolderId = holder?.Id, RegionId = region };
        world.Relics.Add(relic);
        return relic;
    }

    [TestMethod]
    public void TemplatesHaveThirtyDistinctIdsAndSixEqualEffectGroups()
    {
        Assert.HasCount(30, PersonalRelics.Templates);
        Assert.HasCount(30, PersonalRelics.Templates.Select(t => t.Id).Distinct());
        Assert.IsTrue(PersonalRelics.Templates.GroupBy(t => (t.Category, t.Tier)).All(g => g.Count() == 5 && g.Select(t => t.Bonus).Distinct().Count() == 1));
    }

    [TestMethod]
    public void GiftKeepsSelectionWhenFocusIsRemovedAndCanRestoreStewardship()
    {
        var world = WorldFactory.Create("qingyun", 42, "rules", "normal", compact: true);
        world.PendingEvent = null;
        var person = GameEngine.Members(world, world.Player).First();
        person.Focused = true;
        var relic = AddRelic(world, "attack_1_1");
        GameEngine.Validate(world);
        GameEngine.Command(world, new GameCommand("relic_give", person.Id.ToString(), relic.Id.ToString()));
        Assert.AreEqual(person.Id, relic.HolderId);
        Assert.IsTrue(relic.KeepAssigned);
        GameEngine.Command(world, new GameCommand("focus", person.Id.ToString(), "off"));
        Assert.IsTrue(relic.KeepAssigned);
        GameEngine.Command(world, new GameCommand("relic_auto", person.Id.ToString()));
        Assert.IsFalse(relic.KeepAssigned);
        GameEngine.Validate(world.Copy());
    }

    [TestMethod]
    public void InvalidHistoryAndDuplicateHolderAreRejected()
    {
        var world = WorldFactory.Create("qingyun", 43, "rules", "normal", compact: true);
        var person = GameEngine.Members(world, world.Player).First();
        var first = AddRelic(world, "attack_1_1", person);
        GameEngine.Validate(world);
        var unrelated = GameEngine.Log(world, "world", "无关", "无关", []);
        var actualOrigin = first.OriginEvent;
        first.OriginEvent = unrelated;
        Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
        first.OriginEvent = actualOrigin;
        var second = AddRelic(world, "protection_1_1", person);
        Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
        second.HolderId = null;
        world.History.Single(e => e.Id == second.LastEventId).RelicChange!.ToHolderId = null;
        first.LastEventId = second.LastEventId;
        Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
    }

    [TestMethod]
    public void SuccessfulJourneyAutomaticallyDiscoversAndPersistsRelic()
    {
        var world = WorldFactory.Create("qingyun", 717, "rules", "normal", compact: true);
        world.PendingEvent = null;
        world.NextEventMonth = 1000;
        foreach (var p in world.People) p.NextJourneyMonth = 1000;
        var person = GameEngine.Members(world, world.Player).First(p => p.Role != "掌门");
        person.Realm = 2;
        var region = world.Regions.Single(r => r.Id == world.Player.Capital);
        var origin = GameEngine.Log(world, "journey", "下山", "下山", [world.PlayerId]);
        world.Expeditions.Add(new Expedition { Id = world.NewId(), SectId = world.PlayerId, DiscipleId = person.Id,
            RegionId = region.Id, MonthsRemaining = 1, OriginEvent = origin });
        GameEngine.AdvanceMonth(world);
        Assert.IsTrue(person.Alive, "固定种子的测试人物应成功归山。");
        var relic = GameEngine.HeldRelic(world, person.Id);
        Assert.IsNotNull(relic);
        Assert.AreEqual("personal", relic.Scope);
        Assert.AreEqual(2, PersonalRelics.Find(relic.TemplateId)!.Tier);
        Assert.AreEqual(relic.OriginEvent, relic.LastEventId);
        GameEngine.Validate(world.Copy());
    }

    [TestMethod]
    public void LostRelicCannotTeleportAwayFromItsLastEvent()
    {
        var world = WorldFactory.Create("qingyun", 719, "rules", "normal", compact: true);
        var relic = AddRelic(world, "attack_1_1");
        var region = relic.RegionId;
        var lost = GameEngine.Log(world, "relic", "遗失", "遗失", [world.PlayerId], causes: [relic.LastEventId]);
        world.History.Single(e => e.Id == lost).RelicChange = new RelicChange { RelicId = relic.Id, FromOwner = world.PlayerId, ToOwner = "", RegionId = region };
        relic.Owner = ""; relic.LastEventId = lost;
        GameEngine.Validate(world);
        relic.RegionId = world.Regions.First(r => r.Id != region).Id;
        Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
    }

    [TestMethod]
    public void StewardUpgradesAndPassesOldRelicThenInheritsAfterDeathWithoutCommands()
    {
        var world = WorldFactory.Create("qingyun", 715, "rules", "normal", compact: true);
        world.PendingEvent = null; world.AutoStewardship = false; world.NextEventMonth = int.MaxValue;
        var members = GameEngine.Members(world, world.Player);
        foreach (var member in members) { member.Realm = 0; member.RealmMonths = 0; member.AgeMonths = 120; member.Focused = false; }
        var first = members[0]; var next = members[1]; first.Realm = 1; first.Focused = true; next.Focused = true;
        var old = AddRelic(world, "attack_1_1", first);
        var better = AddRelic(world, "attack_2_1");
        GameEngine.AdvanceMonth(world);
        Assert.AreEqual(first.Id, better.HolderId);
        Assert.AreEqual(next.Id, old.HolderId, "同一安全节点应继续分配换下的低阶旧物。");
        next.Realm = 1;
        first.AgeMonths = WorldFactory.Lifespans[first.Realm] * 12 - 1;
        GameEngine.AdvanceMonth(world);
        Assert.IsFalse(first.Alive);
        Assert.AreEqual(next.Id, better.HolderId, "寿尽后同一 ID 应回库并由合格后人继承。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void AwayGrantIsAtomicAndRestoreDefersReplacementUntilReturn()
    {
        var world = WorldFactory.Create("qingyun", 716, "rules", "normal", compact: true);
        world.PendingEvent = null;
        var person = GameEngine.Members(world, world.Player).First(); person.Realm = 1; person.Focused = true;
        var held = AddRelic(world, "attack_1_1", person); held.KeepAssigned = true;
        var better = AddRelic(world, "attack_2_1");
        var departure = GameEngine.Log(world, "journey", "出发", "出发", [world.PlayerId]);
        world.Expeditions.Add(new Expedition { Id = world.NewId(), DiscipleId = person.Id, SectId = world.PlayerId, RegionId = world.Player.Capital, MonthsRemaining = 12, OriginEvent = departure });
        var before = System.Text.Json.JsonSerializer.Serialize(world);
        Assert.Throws<GameRuleException>(() => GameEngine.Command(world, new GameCommand("relic_give", person.Id.ToString(), better.Id.ToString())));
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(world), "失败赐予不得留下部分变更。");
        GameEngine.Command(world, new GameCommand("relic_auto", person.Id.ToString()));
        Assert.IsFalse(held.KeepAssigned);
        Assert.AreEqual(person.Id, held.HolderId);
        Assert.IsNull(better.HolderId, "在外恢复托管不得立即隔空换装。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void JourneyDeathUsesActualGuardianSurvivalForRelicDestination(bool guardianDies)
    {
        var world = WorldFactory.Create("qingyun", 722, "rules", "normal", compact: true);
        world.PendingEvent = null; world.AutoStewardship = false;
        var members = GameEngine.Members(world, world.Player).Where(p => p.Id != world.Player.LeaderId).Take(2).ToArray();
        var person = members[0]; var guardian = members[1]; person.Realm = 0; guardian.Realm = 1;
        var region = world.Regions.Single(r => r.Id == world.Player.Capital); region.Spirit = 5;
        var personRelic = AddRelic(world, "attack_1_1", person); var guardianRelic = AddRelic(world, "attack_1_2", guardian);
        var origin = GameEngine.Log(world, "journey", "下山", person.Name + "与护道人下山", [world.PlayerId]);
        world.Expeditions.Add(new Expedition { Id = world.NewId(), DiscipleId = person.Id, GuardianId = guardian.Id, SectId = world.PlayerId, RegionId = region.Id, MonthsRemaining = 1, OriginEvent = origin });
        uint seed = 1;
        for (; seed < 1_000_000; seed++)
        {
            world.RandomState = seed;
            if (world.Random() < .04 && (world.Random() < .08) == guardianDies) break;
        }
        Assert.IsLessThan(1_000_000u, seed); world.RandomState = seed;
        typeof(GameEngine).GetMethod("AdvanceExpeditions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [world]);
        Assert.IsFalse(person.Alive); Assert.AreEqual(!guardianDies, guardian.Alive);
        Assert.AreEqual(guardianDies ? "" : world.PlayerId, personRelic.Owner);
        var recovery = world.History.Single(e => e.RelicChange?.RelicId == personRelic.Id && e.RelicChange.FromHolderId == person.Id).RelicChange!;
        Assert.IsNull(recovery.ToHolderId); Assert.AreEqual(guardianDies ? "" : world.PlayerId, recovery.ToOwner); Assert.AreEqual(region.Id, recovery.RegionId);
        if (guardianDies) { Assert.IsNull(personRelic.HolderId); Assert.AreEqual(region.Id, personRelic.RegionId); }
        Assert.AreEqual(guardianDies ? "" : world.PlayerId, guardianRelic.Owner);
        Assert.AreEqual(guardianDies ? null : guardian.Id, guardianRelic.HolderId);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void VictoryCapturesTheDeadAndInventoryButLivingSurvivorsKeepRelicsUntilUnattachedDeath()
    {
        var baseWorld = WorldFactory.Create("qingyun", 723, "rules", "normal", compact: true);
        baseWorld.PendingEvent = null; baseWorld.AutoStewardship = false; baseWorld.NextEventMonth = int.MaxValue;
        var enemy = baseWorld.Sects.Single(s => s.Id == "chixia");
        var field = baseWorld.Regions.Single(r => r.Id == enemy.Capital);
        foreach (var land in baseWorld.Regions.Where(r => r.Owner == enemy.Id && r.Id != field.Id)) land.Owner = baseWorld.PlayerId;
        foreach (var fighter in GameEngine.Members(baseWorld, baseWorld.Player)) { fighter.Realm = 5; fighter.Injury = 0; }
        var defenders = GameEngine.Members(baseWorld, enemy);
        foreach (var fighter in defenders) { fighter.Realm = 0; fighter.Injury = 0; }
        var ids = new Dictionary<int, int>();
        foreach (var fighter in defenders)
        {
            var relic = new Relic { Id = baseWorld.NewId(), Scope = "personal", TemplateId = "attack_1_1", Name = "听雨剑", Owner = enemy.Id, HolderId = fighter.Id, RegionId = field.Id, FoundMonth = baseWorld.Month };
            baseWorld.Relics.Add(relic);
            var root = GameEngine.Log(baseWorld, "relic_discovery", "得宝", fighter.Name + "得宝", [enemy.Id]);
            baseWorld.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = enemy.Id, ToHolderId = fighter.Id, RegionId = field.Id };
            relic.OriginEvent = relic.LastEventId = root; ids.Add(fighter.Id, relic.Id);
        }
        var stock = new Relic { Id = baseWorld.NewId(), Scope = "personal", TemplateId = "protection_1_1", Name = "青木护符", Owner = enemy.Id, RegionId = field.Id, FoundMonth = baseWorld.Month };
        baseWorld.Relics.Add(stock);
        var stockRoot = GameEngine.Log(baseWorld, "relic_discovery", "得宝", "旧物入库", [enemy.Id]);
        baseWorld.History[^1].RelicChange = new RelicChange { RelicId = stock.Id, ToOwner = enemy.Id, RegionId = field.Id };
        stock.OriginEvent = stock.LastEventId = stockRoot;
        var away = defenders[^1]; var awayRegion = field.Neighbors.First();
        baseWorld.Relics.Single(r => r.HolderId == away.Id).RegionId = awayRegion;
        var departure = GameEngine.Log(baseWorld, "journey", "在外历练", away.Name + "远行", [enemy.Id]);
        baseWorld.Expeditions.Add(new Expedition { Id = baseWorld.NewId(), DiscipleId = away.Id, SectId = enemy.Id, RegionId = awayRegion, MonthsRemaining = 4, OriginEvent = departure });
        var battle = typeof(GameEngine).GetMethod("Battle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        GameState? result = null;
        for (uint seed = 1; seed <= 100; seed++)
        {
            var trial = baseWorld.Copy(); trial.RandomState = seed;
            battle.Invoke(null, [trial, trial.Player, trial.Regions.Single(r => r.Id == field.Id), "balanced", 1d, true, null, 1d, null]);
            if (trial.Sects.Single(s => s.Id == enemy.Id).Eliminated && defenders.Any(p => !trial.People.Single(t => t.Id == p.Id).Alive) && defenders.Any(p => p.Id != away.Id && trial.People.Single(t => t.Id == p.Id).Alive)) { result = trial; break; }
        }
        Assert.IsNotNull(result, "夹具应找到战死者与生还者并存的确定性战役。");
        Assert.AreEqual(result.PlayerId, result.Relics.Single(r => r.Id == stock.Id).Owner);
        foreach (var (id, relicId) in ids)
        {
            var fighter = result.People.Single(p => p.Id == id); var relic = result.Relics.Single(r => r.Id == relicId);
            Assert.AreEqual(fighter.Alive ? "" : result.PlayerId, relic.Owner);
            Assert.AreEqual(fighter.Alive ? id : null, relic.HolderId);
        }
        var awayRelic = result.Relics.Single(r => r.HolderId == away.Id);
        Assert.AreEqual("", awayRelic.Owner); Assert.AreEqual(awayRegion, awayRelic.RegionId, "在外散修必须保留真实活动地点");
        var survivor = result.People.First(p => ids.ContainsKey(p.Id) && p.Alive && p.Id != away.Id);
        var survivingRelic = result.Relics.Single(r => r.Id == ids[survivor.Id]);
        survivor.AgeMonths = WorldFactory.Lifespans[survivor.Realm] * 12 - 1;
        GameEngine.AdvanceMonth(result);
        Assert.IsFalse(survivor.Alive); Assert.AreEqual("", survivingRelic.Owner); Assert.IsNull(survivingRelic.HolderId);
        Assert.AreEqual(field.Id, survivingRelic.RegionId);
        GameEngine.Validate(result);
    }

    [TestMethod]
    public void LastDefenderDeathStillCapturesTheDefeatedSectInventory()
    {
        var initial = WorldFactory.Create("qingyun", 724, "rules", "normal", compact: true);
        initial.PendingEvent = null; var enemy = initial.Sects.Single(s => s.Id == "chixia");
        var field = initial.Regions.Single(r => r.Id == enemy.Capital);
        foreach (var land in initial.Regions.Where(r => r.Owner == enemy.Id && r.Id != field.Id)) land.Owner = initial.PlayerId;
        foreach (var fighter in GameEngine.Members(initial, initial.Player)) fighter.Realm = 5;
        var enemies = GameEngine.Members(initial, enemy); var last = enemies[0];
        foreach (var fighter in enemies.Skip(1)) fighter.Alive = false;
        enemy.LeaderId = last.Id; last.Role = "掌门"; last.Realm = 0;
        var stock = new Relic { Id = initial.NewId(), Scope = "personal", TemplateId = "protection_1_1", Name = "青木护符", Owner = enemy.Id, RegionId = field.Id, FoundMonth = initial.Month };
        initial.Relics.Add(stock);
        var root = GameEngine.Log(initial, "relic_discovery", "得宝", "旧物入库", [enemy.Id]);
        initial.History[^1].RelicChange = new RelicChange { RelicId = stock.Id, ToOwner = enemy.Id, RegionId = field.Id };
        stock.OriginEvent = stock.LastEventId = root;
        var battle = typeof(GameEngine).GetMethod("Battle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        GameState? result = null;
        for (uint seed = 1; seed <= 100; seed++)
        {
            var trial = initial.Copy(); trial.RandomState = seed;
            battle.Invoke(null, [trial, trial.Player, trial.Regions.Single(r => r.Id == field.Id), "balanced", 1d, true, null, 1d, null]);
            if (!trial.People.Single(p => p.Id == last.Id).Alive) { result = trial; break; }
        }
        Assert.IsNotNull(result); Assert.IsTrue(result.Sects.Single(s => s.Id == enemy.Id).Eliminated);
        Assert.AreEqual(result.PlayerId, result.Relics.Single(r => r.Id == stock.Id).Owner);
        GameEngine.Validate(result);
    }

    [TestMethod]
    public void RecoveredTreasuryReturnsToCapitalAndNaturalDissolutionLosesItThere()
    {
        var world = WorldFactory.Create("qingyun", 900, "rules", "normal", compact: true);
        world.PendingEvent = null; world.NextEventMonth = 1000;
        var sect = world.Sects.First(s => s.Id != world.PlayerId); var capital = sect.Capital;
        var remote = world.Regions.Single(r => r.Id == capital).Neighbors.First();
        foreach (var person in world.People) person.NextJourneyMonth = 1000;
        foreach (var person in GameEngine.Members(world, sect)) { person.Realm = 0; person.RealmMonths = 0; person.AgeMonths = 240; person.Progress = 0; }
        var disciple = GameEngine.Members(world, sect).First(p => p.Role != "掌门");
        var relic = new Relic { Id = world.NewId(), Scope = "personal", TemplateId = "attack_3_1", Name = "惊雷尺", Owner = sect.Id, RegionId = remote, FoundMonth = world.Month };
        world.Relics.Add(relic);
        var origin = GameEngine.Log(world, "relic_discovery", "得宝", "遗宝", [sect.Id]);
        world.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = sect.Id, RegionId = remote }; relic.OriginEvent = relic.LastEventId = origin;
        var lost = GameEngine.Log(world, "relic", "遗失", "远方遗失", [sect.Id], causes: [origin]);
        world.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, FromOwner = sect.Id, RegionId = remote }; relic.Owner = ""; relic.LastEventId = lost;
        var departure = GameEngine.Log(world, "journey", "下山", "远方历练", [sect.Id]);
        world.Expeditions.Add(new Expedition { Id = world.NewId(), SectId = sect.Id, DiscipleId = disciple.Id, RegionId = remote, MonthsRemaining = 1, OriginEvent = departure });
        List<AgentDecision> Quiet() => world.Sects.Where(s => s.Id != world.PlayerId && !s.Eliminated).Select(s => new AgentDecision { SectId = s.Id, Policy = "balanced", Reason = "测试" }).ToList();
        GameEngine.AdvanceMonth(world, Quiet());
        Assert.IsTrue(disciple.Alive); Assert.AreEqual(sect.Id, relic.Owner); Assert.IsNull(relic.HolderId);
        Assert.AreEqual(capital, relic.RegionId, "归山库存当前位置应是山门");
        Assert.AreEqual(remote, world.History.Single(e => e.Id == relic.LastEventId).RelicChange!.RegionId, "历史仍应保留实际拾得地点");
        foreach (var person in GameEngine.Members(world, sect)) person.AgeMonths = WorldFactory.Lifespans[person.Realm] * 12 - 1;
        GameEngine.AdvanceMonth(world, Quiet());
        Assert.IsTrue(sect.Eliminated); Assert.AreEqual("", relic.Owner); Assert.IsNull(relic.HolderId); Assert.AreEqual(capital, relic.RegionId);
        GameEngine.Validate(world);
    }
}
