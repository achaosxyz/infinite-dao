namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    public static List<Cultivator> Members(GameState world, Sect sect) => world.People.Where(p => p.Alive && p.SectId == sect.Id).ToList();
    public static List<Region> Lands(GameState world, Sect sect) => world.Regions.Where(r => r.Owner == sect.Id).ToList();
    public static int Power(GameState world, Sect sect)
    {
        double[] weights = [5, 16, 48, 135, 350, 850, 2100, 5200];
        var basePower = Members(world, sect).Sum(p => weights[p.Realm] * (1 - p.Injury / 140d) * (0.8 + p.Potential / 200d)
            * WorldFactory.InnateCombatMultipliers[p.InnateTrait] * (ActiveRelicTemplate(world, p) is { Category: "attack" } t ? 1 + t.Bonus : 1));
        var relicPower = world.Relics.Where(r => r.Scope == "sect" && r.Owner == sect.Id).OrderByDescending(r => r.Power).Take(3).Sum(r => r.Power);
        return (int)((basePower + relicPower) * (1 + sect.Training * 0.09) * TraitCombatMultiplier(sect) * (0.65 + sect.Stability / 220d) * (1 - sect.WarExhaustion * .004));
    }
    private static double TraitCombatMultiplier(Sect sect) => sect.Trait switch { "sword" => 1.12, "fire" => 1.08, "thunder" => 1.10, _ => 1 };
    public static int Income(GameState world, Sect sect) => (int)(Lands(world, sect).Sum(r => 12 + r.Mine * 7 + r.Development * 8)
        * (sect.Policy == "prosperity" ? 14 : 10) / 10d * FoundationMultiplier(sect)) + (sect.Trait == "forge" ? 15 : 0);
    public static int Upkeep(GameState world, Sect sect) => Members(world, sect).Sum(p => CultivatorStoneUpkeep[p.Realm]) + Math.Max(0, Lands(world, sect).Count - (8 + sect.Doctrine * 4)) * 12;
    public static int HerbIncome(GameState world, Sect sect) => (int)(Lands(world, sect).Sum(r => 2 + r.HerbYield * 3 + r.Development)
        * (sect.Trait == "herb" ? 14 : 10) / 10d * FoundationMultiplier(sect));
    public static int Log(GameState world, string kind, string title, string text, string[] sects, string delta = "", int[]? causes = null)
    {
        var id = world.NewId();
        world.History.Add(new Chronicle { Id = id, Month = world.Month, Kind = kind, Title = title, Text = text, Sects = sects.ToList(), Delta = delta, Causes = causes?.ToList() ?? [] });
        return id;
    }
    public static bool Border(GameState world, Sect sect, Region region) => region.Neighbors.Any(n => world.Regions.Any(r => r.Id == n && r.Owner == sect.Id));
    public static bool AtPeace(GameState world, Sect a, Sect b) => a.Truces.GetValueOrDefault(b.Id) > world.Month;
    public static void Spend(Sect sect, int stones, int herbs = 0)
    {
        if (sect.Stones < stones) throw new GameRuleException($"灵石不足，需要 {stones}，当前 {sect.Stones}。");
        if (sect.Herbs < herbs) throw new GameRuleException($"灵材不足，需要 {herbs}，当前 {sect.Herbs}。");
        sect.Stones -= stones; sect.Herbs -= herbs;
    }

    public static void Command(GameState world, GameCommand command)
    {
        if (world.Status != "playing") throw new GameRuleException("此局已结束，可以查看终局或开始新局。");
        var sect = world.Player;
        if (command.Type == "mode")
        {
            if (command.Option is not ("llm" or "rules")) throw new GameRuleException("世界模式无效。");
            world.Mode = command.Option;
            Log(world, "decision", "世界模式已切换", command.Option == "llm" ? "后续战略议事将由大模型提出提案，事实仍由世界规则结算。" : "明确切换为离线规则世界，不再发送模型请求。", [sect.Id]);
            return;
        }
        if (world.PendingEvent is not null)
        {
            if (command.Type != "choose") throw new GameRuleException("请先处理当前宗门议事。");
            Choose(world, command.Option); CheckEnd(world); Validate(world); return;
        }
        if (command.Type == "choose") throw new GameRuleException("当前没有待处理的议事。");
        if (command.Type == "policy")
        {
            if (!WorldFactory.Policies.Contains(command.Option)) throw new GameRuleException("发展策略无效。");
            sect.Policy = command.Option;
            Log(world, "decision", "宗令更易", $"{sect.Name}转向「{WorldFactory.PolicyNames[Array.IndexOf(WorldFactory.Policies, command.Option)]}」。", [sect.Id]);
            return;
        }
        if (command.Type is "focus" or "guardian")
        {
            AssignDisciple(world, command); Validate(world); return;
        }
        if (command.Type is "relic_give" or "relic_auto")
        {
            AssignPersonalRelic(world, command); Validate(world); return;
        }
        if (sect.Exiled && command.Type != "settle") throw new GameRuleException("宗门正在流亡，请先选择落脚之地重建山门。");
        if (world.CommandsLeft <= 0) throw new GameRuleException("本月两道宗令已下达，请推进时间后再作安排。");
        switch (command.Type)
        {
            case "develop":
            {
                var region = OwnRegion(world, sect, command.Target);
                if (region.Development >= 5) throw new GameRuleException("该地已发展至最高等级。");
                var cost = region.Development * 100 + 80; Spend(sect, cost);
                region.Development++;
                Log(world, "economy", "兴建灵田与坊市", $"{region.Name}发展至 {region.Development} 级，每月产出随之增加。", [sect.Id], $"灵石 -{cost}；发展 +1"); break;
            }
            case "fortify":
            {
                var region = OwnRegion(world, sect, command.Target);
                if (region.Fort >= 5) throw new GameRuleException("护山阵已达最高等级。");
                Spend(sect, region.Fort * 100 + 60, 15); region.Fort++;
                Log(world, "economy", "护山大阵加固", $"{region.Name}的防守倍率提升，入侵者将付出更多代价。", [sect.Id], "阵法 +1"); break;
            }
            case "recruit":
            {
                if (Members(world, sect).Count >= 100) throw new GameRuleException("宗门已达百人编制，请先培养现有门徒。");
                var land = Lands(world, sect).OrderByDescending(r => r.Population).FirstOrDefault() ?? throw new GameRuleException("没有可招募弟子的领地。");
                if (land.Population < 30) throw new GameRuleException("此地适龄人口不足。");
                Spend(sect, 140); var count = Math.Min(4, 100 - Members(world, sect).Count);
                for (var i = 0; i < count; i++) WorldFactory.NewCultivator(world, sect.Id);
                land.Population -= count;
                Log(world, "people", "山门纳新", $"{count} 名新弟子拜入{sect.Name}。", [sect.Id], $"灵石 -140；弟子 +{count}"); break;
            }
            case "train":
                if (sect.Training >= 10) throw new GameRuleException("军阵已臻化境。");
                var trainingCost = TrainingUpgradeCost(sect);
                Spend(sect, trainingCost.Stones, trainingCost.Herbs); sect.Training++;
                foreach (var p in Members(world, sect)) p.Progress = Math.Min(120, p.Progress + 8);
                Log(world, "cultivation", "演武传功", $"军阵升至 {sect.Training} 级，全体修士获得修为进展。", [sect.Id], "军阵 +1；修为 +8"); break;
            case "research":
                if (sect.Doctrine >= 8) throw new GameRuleException("道藏已达当前世界巅峰。");
                var doctrineCost = DoctrineUpgradeCost(sect);
                Spend(sect, doctrineCost.Stones, doctrineCost.Herbs); sect.Doctrine++;
                Log(world, "cultivation", "道藏推演", $"{sect.Name}道藏升至 {sect.Doctrine} 阶，长期修炼效率提高。", [sect.Id], "道藏 +1"); break;
            case "breakthrough":
            {
                if (!int.TryParse(command.Target, out var id)) throw new GameRuleException("人物无效。");
                var person = Members(world, sect).SingleOrDefault(p => p.Id == id) ?? throw new GameRuleException("无法指派此人突破。");
                if (!CanAdvanceRealm(person) || person.Progress < 70 || person.Injury > 40 || person.RealmMonths < WorldFactory.MinimumRealmMonths[person.Realm]) throw new GameRuleException("突破需未达先天特性上限、修为达到70、伤势不超过40，且当前境龄已满。连续演武不能跨过境龄限制。");
                var cost = BreakthroughCost(person, true);
                Spend(sect, cost.Stones, cost.Herbs);
                Breakthrough(world, sect, person, true); break;
            }
            case "heal":
                Spend(sect, 50, 50);
                foreach (var p in Members(world, sect)) p.Injury = Math.Max(0, p.Injury - 45);
                sect.Stability = Math.Min(100, sect.Stability + 6);
                Log(world, "people", "开炉济伤", "丹房开炉，全宗伤势减轻，众心渐定。", [sect.Id], "灵石 -50；灵材 -50；伤势 -45"); break;
            case "explore":
                Spend(sect, 120, 10); Explore(world, sect); break;
            case "scout":
                Spend(sect, 70); sect.Intel = Math.Min(5, sect.Intel + 1);
                Log(world, "diplomacy", "游子布眼", "暗探回报了各宗战略倾向。情报等级提高，出征战力加成增加。", [sect.Id], "灵石 -70；情报 +1"); break;
            case "gift":
            case "truce":
            {
                var other = OtherSect(world, command.Target);
                if (other.Eliminated) throw new GameRuleException("此宗道统已断。");
                var relation = sect.Relations.GetValueOrDefault(other.Id);
                if (command.Type == "truce" && relation < 15) throw new GameRuleException("关系需达到 15 才能缔结盟约。可先馈赠灵石。");
                Spend(sect, command.Type == "gift" ? 120 : 180);
                if (command.Type == "gift")
                {
                    other.Stones += 100; ChangeRelation(sect, other, 25);
                    Log(world, "diplomacy", "山门赠礼", $"向{other.Name}送去灵石与书信，双方关系改善。", [sect.Id, other.Id], "灵石 -120；对方 +100；关系 +25");
                }
                else
                {
                    sect.Truces[other.Id] = world.Month + 18; other.Truces[sect.Id] = world.Month + 18;
                    Log(world, "diplomacy", "十八月不战之盟", $"{sect.Name}与{other.Name}缔结互不侵犯之约。", [sect.Id, other.Id], "灵石 -180；停战 18 月");
                }
                break;
            }
            case "withdraw":
            {
                var campaign = world.Campaigns.SingleOrDefault(c => c.Id.ToString() == command.Target && c.Attacker == sect.Id) ?? throw new GameRuleException("没有可撤回的本宗远征军。");
                world.Campaigns.Remove(campaign); sect.WarExhaustion = Math.Min(100, sect.WarExhaustion + 6);
                ReturnCampaignRelics(world, campaign, sect);
                Log(world, "war", "主动收兵", $"{sect.Name}撤回远征军，已支付的补给与动员费用不返还。", [sect.Id], "战疲 +6", [campaign.OriginEvent]); break;
            }
            case "attack":
            {
                if (IsMobilized(world, sect)) throw new GameRuleException("主力正在远征，无法同时开启第二场进攻。");
                var target = world.Regions.SingleOrDefault(r => r.Id == command.Target) ?? throw new GameRuleException("目标地域不存在。");
                if (world.Campaigns.Any(c => c.Target == target.Id)) throw new GameRuleException("该地已有战役进行，请查看战役进展。");
                if (target.Owner == sect.Id || !Border(world, sect, target)) throw new GameRuleException("只能出征与本宗相邻的他宗或无主地域。");
                if (target.Owner != "" && AtPeace(world, sect, OtherSect(world, target.Owner))) throw new GameRuleException("盟约尚未到期，不能背盟出征。");
                if (sect.Stones < 130 || sect.Herbs < 20) throw new GameRuleException("出征需预备 130 灵石与 20 灵材。");
                world.PendingEvent = BattleCouncil(world, sect, target, false);
                return;
            }
            case "retention":
            {
                var person = Members(world, sect).SingleOrDefault(p => p.Id.ToString() == command.Target) ?? throw new GameRuleException("只能安排本宗在世门人。");
                if (person.Realm != WorldFactory.MaximumRealm || command.Option is not ("on" or "off")) throw new GameRuleException("只有大乘修士可以选择留世或顺应飞升。");
                person.RetainInWorld = command.Option == "on"; person.AscensionWarned = true;
                Log(world, "decision", person.RetainInWorld ? "议定留世" : "顺应飞升", $"{person.Name}{(person.RetainInWorld ? "大乘满十年后逐月供给遮天灵阵；资源不足则自动飞升。" : "大乘满十年后自动飞升，遗宝传承、底蕴归宗。")}", [sect.Id]);
                break;
            }
            case "settle":
                Settle(world, sect, command.Target); break;
            default: throw new GameRuleException("未知宗令。");
        }
        if (!sect.Exiled && !sect.Eliminated) AssignInventory(world, sect);
        world.CommandsLeft--; CheckEnd(world); Validate(world);
    }

    private static Region OwnRegion(GameState world, Sect sect, string id) => world.Regions.SingleOrDefault(r => r.Id == id && r.Owner == sect.Id) ?? throw new GameRuleException("只能建设本宗领地。");
    private static Sect OtherSect(GameState world, string id) => world.Sects.SingleOrDefault(s => s.Id == id && s.Id != world.PlayerId) ?? throw new GameRuleException("请选择其他宗门。");
    public static void ChangeRelation(Sect a, Sect b, int delta)
    {
        a.Relations[b.Id] = Math.Clamp(a.Relations.GetValueOrDefault(b.Id) + delta, -100, 100);
        b.Relations[a.Id] = Math.Clamp(b.Relations.GetValueOrDefault(a.Id) + delta, -100, 100);
    }

    private static void Breakthrough(GameState world, Sect sect, Cultivator person, bool prepared)
    {
        var chance = Math.Clamp(.25 + person.Potential / 250d + sect.Doctrine * .02 + person.InnateTrait * .065 - person.Realm * .035 + (prepared ? .12 : 0), .15, .96);
        person.Progress = 10;
        if (world.Random() < chance)
        {
            person.Realm++; person.RealmMonths = 0; person.Injury = Math.Max(0, person.Injury - 10); sect.Renown += 5;
            person.Biography = $"青岚历 {world.Month / 12 + 1} 年突破至{WorldFactory.RealmNames[person.Realm]}。";
            Log(world, "cultivation", $"{person.Name}破境", $"天光垂落，{person.Name}晋入{WorldFactory.RealmNames[person.Realm]}，寿元上限增至 {WorldFactory.Lifespans[person.Realm]} 年。", [sect.Id], "境界 +1；威望 +5");
        }
        else
        {
            person.Injury = Math.Min(90, person.Injury + 25);
            Log(world, "cultivation", "破境未成", $"{person.Name}冲关失败，根基受损，需要静养；已投入资源无法追回。", [sect.Id], "修为重置；伤势 +25");
        }
    }

    public static void Validate(GameState world)
    {
        if (world.SchemaVersion != 4) throw new GameRuleException("存档版本不兼容；此版需要新开局，旧档文件不会被修改。");
        if (world.Sects.Count != 10 || world.MapColumns is < 4 or > 64 || world.MapRows is < 4 or > 64 || world.Regions.Count != world.MapColumns * world.MapRows || world.Month < 0 || world.Revision < 0) throw new GameRuleException("存档结构无效。");
        if (world.People.Count > 100000 || world.History.Count > 1000000 || world.Decisions.Count > 2000000) throw new GameRuleException("存档数据量超出安全限制。");
        if (world.Sects.Select(s => s.Id).Distinct().Count() != world.Sects.Count || world.Regions.Select(r => r.Id).Distinct().Count() != world.Regions.Count || world.Regions.Select(r => r.Index).Distinct().Count() != world.Regions.Count || world.People.Select(p => p.Id).Distinct().Count() != world.People.Count) throw new GameRuleException("实体身份重复。");
        if (!world.Sects.Any(s => s.Id == world.PlayerId)) throw new GameRuleException("玩家宗门不存在。");
        foreach (var sect in world.Sects)
        {
            if (sect.Stones < 0 || sect.Herbs < 0 || sect.Stability is < 0 or > 100 || sect.Doctrine is < 1 or > 8 || sect.Training is < 1 or > 10 || sect.WarExhaustion is < 0 or > 100 || sect.Foundation is < 0 or > 100000) throw new GameRuleException("宗门资源或属性越界。");
            if (sect.Eliminated && Lands(world, sect).Count > 0) throw new GameRuleException("已灭宗门仍占有领地。");
            if (!sect.Eliminated && !Members(world, sect).Any(p => p.Id == sect.LeaderId)) throw new GameRuleException("掌门身份无效。");
        }
        foreach (var r in world.Regions)
            if ((r.Owner != "" && !world.Sects.Any(s => s.Id == r.Owner)) || r.Development is < 1 or > 5 || r.Fort is < 1 or > 5 || r.Population < 0 || r.Index < 0 || r.Index >= world.Regions.Count || r.Neighbors.Any(n => !world.Regions.Any(x => x.Id == n && x.Neighbors.Contains(r.Id)))) throw new GameRuleException("地图属性或所有权无效。");
        foreach (var p in world.People)
            if (p.Realm < 0 || p.Realm > WorldFactory.MaximumRealm || p.InnateTrait is < 0 or > 4 || p.Ascended && (p.Alive || p.Realm != WorldFactory.MaximumRealm) || p.RetainInWorld && (!p.Alive || p.Realm != WorldFactory.MaximumRealm) || p.Injury is < 0 or > 100 || p.AgeMonths < 0 || p.RealmMonths < 0 || p.Progress is < 0 or > 120 || p.Portrait < 0 || p.Portrait >= WorldFactory.PortraitCount || !world.Sects.Any(s => s.Id == p.SectId) && p.SectId != "") throw new GameRuleException("人物状态无效。");
        if (world.PendingEvent is { } pending && (pending.ResourceScale is < 1 or > 10000 || pending.Choices.Any(c => c.Stones < 0 || c.Herbs < 0)))
            throw new GameRuleException("议事资源规模或费用无效。");
        var seen = new HashSet<int>();
        foreach (var e in world.History)
        {
            if (e.Id <= 0 || e.Id >= world.NextId || e.Causes.Any(c => !seen.Contains(c)) || !seen.Add(e.Id)) throw new GameRuleException("历史因果引用无效。");
        }
        PersonalRelics.Validate(world, seen);
        if (world.Campaigns.Count > world.Sects.Count || world.Campaigns.Select(c => c.Attacker).Distinct().Count() != world.Campaigns.Count || world.Campaigns.Select(c => c.Target).Distinct().Count() != world.Campaigns.Count) throw new GameRuleException("远征军或战线重复。");
        foreach (var c in world.Campaigns)
            if (!world.Sects.Any(s => s.Id == c.Attacker) || !world.Regions.Any(r => r.Id == c.Target) || c.Defender != "" && !world.Sects.Any(s => s.Id == c.Defender) || c.MonthsRemaining < 1 || c.MonthsRemaining > c.Duration || c.Duration < 2 || c.MonthlyStones < 0 || c.MonthlyHerbs < 0 || !double.IsFinite(c.Supply) || c.Supply is < 0 or > 1 || !double.IsFinite(c.DefenseBonus) || c.DefenseBonus is < 1 or > 1.45 || !seen.Contains(c.OriginEvent)) throw new GameRuleException("战役状态无效。");
        var deployed = world.Campaigns.SelectMany(c => c.Participants).ToList();
        foreach (var journey in world.Expeditions)
        {
            if (!world.People.Any(p => p.Id == journey.DiscipleId) || journey.GuardianId == journey.DiscipleId || journey.MonthsRemaining is < 1 or > 36
                || !world.Regions.Any(r => r.Id == journey.RegionId) || !world.Sects.Any(s => s.Id == journey.SectId) || !seen.Contains(journey.OriginEvent)) throw new GameRuleException("历练状态无效。");
            deployed.Add(journey.DiscipleId);
            if (journey.GuardianId is int guardian) deployed.Add(guardian);
        }
        if (deployed.Distinct().Count() != deployed.Count || deployed.Any(id => !world.People.Any(p => p.Id == id))) throw new GameRuleException("门人重复部署或不存在。");
        foreach (var report in world.BattleReports)
            if (!seen.Contains(report.EventId) || !world.Regions.Any(r => r.Id == report.RegionId) || report.Rounds is < 0 or > 48
                || !double.IsFinite(report.FormationBonus) || report.Exchanges.Any(e => !double.IsFinite(e.Damage) || e.Damage < 0 || !double.IsFinite(e.RealmMultiplier) || e.RealmMultiplier <= 0 || e.AttackerRealm < 0 || e.AttackerRealm > (world.SchemaVersion >= 4 ? WorldFactory.MaximumRealm : 5) || e.DefenderRealm < 0 || e.DefenderRealm > (world.SchemaVersion >= 4 ? WorldFactory.MaximumRealm : 5)
                    || e.RelicName is null || e.RelicCategory is not ("" or "attack" or "protection") || e.RelicTier is < 0 or > 2 || !double.IsFinite(e.RelicBonus) || e.RelicBonus is < 0 or > .25 || (e.RelicName == "") != (e.RelicCategory == ""))) throw new GameRuleException("对局战报无效。");
        if (world.AgentStatus is null or { Length: > 600 }) throw new GameRuleException("宗门Agent状态提示无效。");
        if (world.AgentMemories.Select(m => m.SectId).Distinct().Count() != world.AgentMemories.Count) throw new GameRuleException("宗门Agent会话重复。");
        foreach (var memory in world.AgentMemories)
        {
            if (!world.Sects.Any(s => s.Id == memory.SectId && s.Id != world.PlayerId) || memory.Summary.Length > 32000
                || memory.ContextTokens < 0 || memory.Compactions < 0 || memory.LastReviewMonth < -1 || memory.LastReviewMonth > world.Month
                || memory.NextReviewMonth < memory.LastReviewMonth || memory.LastObservedEventId < 0
                || memory.Conversation.Concat(memory.Archive).Any(e => e.Role is not ("user" or "assistant") || e.Text.Length > 2_000_000 || e.Month < 0 || e.Month > world.Month))
                throw new GameRuleException("宗门Agent公开会话无效。");
            if (memory.Plan is not null)
            {
                ValidatePlanShape(memory.Plan);
                if (memory.Plan.Target != "" && !world.Regions.Any(r => r.Id == memory.Plan.Target)) throw new GameRuleException("宗门计划引用了不存在的地域。");
            }
        }
    }
}
