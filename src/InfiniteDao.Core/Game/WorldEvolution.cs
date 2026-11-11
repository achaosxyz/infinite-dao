namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    public static void AdvanceMonth(GameState world, IReadOnlyList<AgentDecision>? decisions = null)
    {
        if (world.Status != "playing") throw new GameRuleException("此局已经结束。");
        if (world.PendingEvent is not null) throw new GameRuleException("请先处理议事再推进世界。");
        if (world.Player.Exiled) throw new GameRuleException("请先为幸存门徒选择重建之地。");
        var chosen = decisions ?? RuleDecisions(world);
        ApplyDecisions(world, chosen);
        world.Month++;
        world.CommandsLeft = 2;
        foreach (var sect in world.Sects.Where(s => !s.Eliminated).OrderBy(s => s.Id))
        {
            if (sect.Exiled) continue;
            if (!IsMobilized(world, sect)) sect.WarExhaustion = Math.Max(0, sect.WarExhaustion - 4);
            var income = Income(world, sect); var upkeep = Upkeep(world, sect);
            sect.Stones += income;
            var lack = Math.Max(0, upkeep - sect.Stones);
            sect.Stones = Math.Max(0, sect.Stones - upkeep);
            sect.Herbs += HerbIncome(world, sect);
            if (world.AutoStewardship) ProvisionSect(world, sect);
            var reservedHerbs = ReservedHerbs(world, sect);
            if (lack > 0)
            {
                sect.Stability = Math.Max(0, sect.Stability - 6);
                foreach (var p in Members(world, sect)) { p.Loyalty = Math.Max(0, p.Loyalty - 3); p.Injury = Math.Min(85, p.Injury + 3); }
                if (world.Month % 3 == 0) Log(world, "economy", "供养告急", $"{sect.Name}月度供养短缺 {lack} 灵石，门中人心浮动。", [sect.Id], $"安定 -6；未支付缺口 {lack}");
            }
            else sect.Stability = Math.Min(100, sect.Stability + (sect.Policy == "prosperity" ? 3 : 1));
            if (sect.Policy == "diplomacy")
            {
                foreach (var other in world.Sects.Where(s => s.Id != sect.Id && !s.Eliminated)) ChangeRelation(sect, other, 1);
                if (world.Month % 6 == 0) sect.Intel = Math.Min(5, sect.Intel + 1);
            }
            foreach (var region in Lands(world, sect))
            {
                var capacity = 50_000 + region.Development * 20_000 + region.Spirit * 6_000;
                if (region.Population < capacity) region.Population += Math.Max(1, (capacity - region.Population) / 500);
            }
            foreach (var person in Members(world, sect).OrderBy(p => p.Id).ToList())
            {
                person.AgeMonths++;
                person.RealmMonths++;
                if (person.AgeMonths >= WorldFactory.Lifespans[person.Realm] * 12)
                {
                    person.Alive = false;
                    person.Biography += $" 青岚历{world.Month / 12 + 1}年寿尽坐化，享年{person.AgeMonths / 12}岁。";
                    Log(world, "death", "仙路有尽", $"{sect.Name}的{person.Role}{person.Name}寿尽坐化，享年 {person.AgeMonths / 12} 岁。", [sect.Id]);
                    continue;
                }
                person.Injury = Math.Max(0, person.Injury - (IsAway(world, person.Id) ? 1 : sect.Trait is "herb" or "body" ? 5 : 3));
                if (NeedsCultivation(person))
                {
                    var herbCost = 1 + person.Realm;
                    var supplied = sect.Herbs >= herbCost + reservedHerbs;
                    if (supplied) sect.Herbs -= herbCost;
                    var progress = (1 + person.Potential / 25 + sect.Doctrine) * (sect.Policy == "cultivation" ? 15 : 10) / 10;
                    if (sect.Trait == "ice") progress += 2;
                    if (sect.Trait == "star") progress++;
                    if (person.Focused && sect.Herbs >= reservedHerbs + 3 + person.Realm * 2)
                    { sect.Herbs -= 3 + person.Realm * 2; progress *= 2; }
                    if (IsAway(world, person.Id)) progress = Math.Max(1, progress / 3);
                    if (!supplied || lack > 0) progress = Math.Max(1, progress / 3);
                    person.Progress = Math.Min(120, person.Progress + progress);
                }
                if (person.Progress >= 110 && person.Realm < 5 && person.RealmMonths >= WorldFactory.MinimumRealmMonths[person.Realm] && !IsAway(world, person.Id) && person.Injury < 30 && sect.Herbs >= reservedHerbs + 30 + person.Realm * 20 && sect.Stones >= 60 + person.Realm * 50)
                {
                    Spend(sect, 60 + person.Realm * 50, 30 + person.Realm * 20);
                    Breakthrough(world, sect, person, false);
                }
            }
            EnsureLeader(world, sect);
            if (sect.Eliminated) continue;
            if (world.AutoStewardship) StewardDevelop(world, sect);
            else if (sect.Id != world.PlayerId) RivalDevelop(world, sect);
        }
        AdvanceCampaigns(world);
        if (world.AutoStewardship) AdvanceExpeditions(world);
        RivalConflict(world);
        if (world.Status == "playing" && world.PendingEvent is null && world.Month >= world.NextEventMonth)
        {
            world.PendingEvent = GenerateEvent(world);
            world.NextEventMonth = world.Month + world.Roll(24, 60);
        }
        CheckEnd(world); Validate(world);
    }

    public static List<AgentDecision> RuleDecisions(GameState world) => world.Sects.Where(s => !s.Eliminated && s.Id != world.PlayerId)
        .OrderBy(s => s.Id).Select(s => new AgentDecision
        {
            Month = world.Month, SectId = s.Id,
            Policy = s.Stones < 180 ? "prosperity" : s.Trait == "fire" ? "expansion" : s.Trait == "ice" ? "cultivation" : s.Policy,
            Target = world.Regions.Where(r => r.Owner != s.Id && Border(world, s, r)).OrderBy(r => r.Owner == "" ? 0 : 1).ThenBy(r => r.Fort).ThenBy(r => r.Id).Select(r => r.Id).FirstOrDefault() ?? "",
            Reason = s.Stones < 180 ? "供养不足，先稳定生产与民心。" : "审视边界与资源，按宗门性格推进既定战略。", Source = "rules"
        }).ToList();

    public static void ApplyDecisions(GameState world, IReadOnlyList<AgentDecision> decisions)
    {
        var eligible = world.Sects.Where(s => !s.Eliminated && s.Id != world.PlayerId).Select(s => s.Id).Order().ToArray();
        if (!eligible.SequenceEqual(decisions.Select(d => d.SectId).Order())) throw new GameRuleException("模型提案未完整覆盖当前存续的其他宗门。");
        foreach (var d in decisions)
        {
            if (!WorldFactory.Policies.Contains(d.Policy) || d.Reason.Length > 300 || d.Reason.Length == 0) throw new GameRuleException("模型返回了无效策略或理由。");
            var sect = world.Sects.Single(s => s.Id == d.SectId);
            if (d.Target != "" && !world.Regions.Any(r => r.Id == d.Target && r.Owner != sect.Id && Border(world, sect, r))) throw new GameRuleException("模型选择了不可达的战略目标。");
        }
        foreach (var d in decisions.OrderBy(d => d.SectId))
        {
            var sect = world.Sects.Single(s => s.Id == d.SectId);
            sect.Policy = d.Policy; sect.Target = d.Target; sect.Intent = d.Reason;
            var decidedMonth = d.Source == "llm" ? d.Month : world.Month;
            if (!world.Decisions.Any(old => old.SectId == d.SectId && old.Month == decidedMonth && old.Policy == d.Policy && old.Target == d.Target && old.Reason == d.Reason && old.Source == d.Source))
                world.Decisions.Add(new AgentDecision { Month = decidedMonth, SectId = d.SectId, Policy = d.Policy, Target = d.Target, Reason = d.Reason, Source = d.Source });
        }
        world.LastCouncil = world.Mode == "llm" ? "诸宗延续既定战略，Agent在后台按需规划，不阻塞岁月流转。" : "诸宗依离线策略完成本月议事。";
    }

    private static void EnsureLeader(GameState world, Sect sect)
    {
        var people = Members(world, sect);
        if (people.Count == 0)
        {
            if (sect.Id == world.PlayerId)
            {
                var heir = WorldFactory.NewCultivator(world, sect.Id);
                heir.Name = "遗脉·" + heir.Name; heir.Biography = "旧宗遗孤，携半卷道藏归来。";
                people.Add(heir);
            }
            else { Dissolve(world, sect); return; }
        }
        if (people.Any(p => p.Id == sect.LeaderId)) return;
        var leader = people.OrderByDescending(p => p.Realm).ThenByDescending(p => p.Loyalty).ThenBy(p => p.Id).First();
        leader.Role = "掌门"; sect.LeaderId = leader.Id; sect.Stability = Math.Max(0, sect.Stability - 12);
        Log(world, "people", "薪传不绝", $"{leader.Name}承继{sect.Name}掌门之位。", [sect.Id], "安定 -12");
    }

    private static void RivalDevelop(GameState world, Sect sect)
    {
        var lands = Lands(world, sect);
        var people = Members(world, sect);
        var capacity = world.Difficulty == "story" ? 55 : world.Difficulty == "hard" ? 100 : 80;
        if (sect.Stones > 300 && people.Count < capacity && lands.Count > 0 && lands[0].Population > 20)
        {
            var count = Math.Min(4, capacity - people.Count);
            Spend(sect, 140); for (var i = 0; i < count; i++) WorldFactory.NewCultivator(world, sect.Id); lands[0].Population -= count;
        }
        if (sect.Policy == "prosperity" && sect.Stones > 400)
        {
            var r = lands.Where(r => r.Development < 5).OrderBy(r => r.Development).FirstOrDefault();
            if (r is not null) { Spend(sect, 180); r.Development++; }
        }
        else if (sect.Policy == "cultivation" && sect.Stones > 220 + sect.Doctrine * 80 + 200 && sect.Herbs > 120 && sect.Doctrine < 8) { Spend(sect, 220 + sect.Doctrine * 80, 60); sect.Doctrine++; }
        else if (sect.Stones > 160 + sect.Training * 60 + 300 && sect.Herbs > 60 && sect.Training < 10) { Spend(sect, 160 + sect.Training * 60, 30); sect.Training++; }
        if (sect.Policy == "diplomacy")
        {
            var other = world.Sects.Where(s => s.Id != sect.Id && !s.Eliminated).OrderByDescending(s => sect.Relations.GetValueOrDefault(s.Id)).FirstOrDefault();
            if (other is not null) ChangeRelation(sect, other, 5);
        }
    }

    private static void RivalConflict(GameState world)
    {
        if (world.AutoStewardship && world.Month % 12 != 0) return;
        if (world.AutoStewardship)
        {
            var player = world.Player;
            player.Target = world.Regions.Where(r => r.Owner != player.Id && Border(world, player, r)
                && (r.Owner == "" || !AtPeace(world, player, world.Sects.Single(s => s.Id == r.Owner))))
                .OrderBy(r => r.Owner == "" ? 0 : 1).ThenBy(r => DefensePower(world, r)).ThenBy(r => r.Id).FirstOrDefault()?.Id ?? "";
        }
        // All candidates use the same committed state. Each sect can participate in only one conflict per round.
        var intents = world.Sects.Where(s => !s.Eliminated && (s.Id != world.PlayerId || world.AutoStewardship) && !s.Exiled && !IsMobilized(world, s) && s.Stones >= 130 && s.Herbs >= 20 && s.WarExhaustion < 25)
            .Select(s => new { Sect = s, Target = world.Regions.FirstOrDefault(r => r.Id == s.Target && r.Owner != s.Id && Border(world, s, r)) })
            .Where(x => x.Target is not null).OrderBy(x => x.Target!.Id).ThenBy(x => x.Sect.Id).ToList();
        var engaged = new HashSet<string>(); var claimed = new HashSet<string>();
        foreach (var intent in intents)
        {
            var sect = intent.Sect; var target = intent.Target!;
            if (engaged.Contains(sect.Id) || claimed.Contains(target.Id) || world.Campaigns.Any(c => c.Target == target.Id) || target.Owner == sect.Id) continue;
            var participants = CampaignMembers(world, sect);
            if (participants.Count == 0) continue;
            var defender = world.Sects.FirstOrDefault(s => s.Id == target.Owner);
            if (defender is not null && (engaged.Contains(defender.Id) || AtPeace(world, sect, defender))) continue;
            var costs = CampaignCosts(world, sect, target);
            if (world.AutoStewardship && (sect.Stones < 130 + costs.Duration * costs.Stones + Upkeep(world, sect) * 6
                || sect.Herbs < 20 + costs.Duration * costs.Herbs)) continue;
            var aggression = sect.Policy == "expansion" ? 0.8 : sect.Policy == "prosperity" ? 0.15 : 0.4;
            if (world.Random() > aggression) continue;
            if (!CanChallengeGarrison(world, sect, target, participants)) continue;
            if (defender?.Id == world.PlayerId)
            {
                if (world.PendingEvent is not null || world.Month < (world.Difficulty == "hard" ? 36 : world.Difficulty == "story" ? 120 : 60)) continue;
                world.PendingEvent = BattleCouncil(world, sect, target, true);
                engaged.Add(sect.Id); engaged.Add(defender.Id); claimed.Add(target.Id);
                Log(world, "war", "边境告急", $"{sect.Name}兵临{target.Name}，等待掌门决断。", [sect.Id, defender.Id]);
                break;
            }
            if (defender is not null && world.Random() > 0.5) continue;
            StartCampaign(world, sect, target, "balanced");
            engaged.Add(sect.Id); if (defender is not null) engaged.Add(defender.Id); claimed.Add(target.Id);
        }
    }

    private static void Explore(GameState world, Sect sect)
    {
        var roll = world.Random();
        if (roll < 0.42)
        {
            var amount = world.Roll(180, 350); sect.Stones += amount; sect.Herbs += 30;
            Log(world, "explore", "秘境有获", "弟子在古洞府中寻得灵石与丹材，已悉数带回山门。", [sect.Id], $"灵石 +{amount}；灵材 +30");
        }
        else if (roll < 0.78)
        {
            string[] relics = ["听雨古剑", "九霄雷印", "碧玉丹鼎", "玄河宝镜", "归藏玉简", "雪魄灵珠"];
            var name = relics[world.Roll(0, 5)]; var power = world.Roll(25, 55);
            var e = Log(world, "relic", "古物重见天日", $"探索队寻得「{name}」。此物从此与本宗历史相连。", [sect.Id], $"战力 +{power}");
            world.Relics.Add(new Relic { Id = world.NewId(), Name = name, Owner = sect.Id, Power = power, Effect = "镇守道统，提升全宗出征与防守实力", FoundMonth = world.Month, OriginEvent = e });
        }
        else
        {
            foreach (var p in Members(world, sect).OrderBy(p => p.Id).Take(4)) p.Injury = Math.Min(80, p.Injury + 25);
            sect.Herbs += 50;
            Log(world, "explore", "险地归来", "探索队遭遇妖兽，数人受伤，仍带回了 50 灵材。", [sect.Id], "部分弟子伤势 +25；灵材 +50");
        }
    }
}
