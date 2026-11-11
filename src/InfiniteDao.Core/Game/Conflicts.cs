namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    private static GameEvent BattleCouncil(GameState world, Sect attacker, Region region, bool defend) => new()
    {
        Id = world.NewId(), Type = defend ? "defense" : "attack", Title = defend ? $"{region.Name}，风雨将至" : $"出征{region.Name}",
        Eyebrow = defend ? "边境急报 · 世界已暂停" : "出征议事 · 选择军阵", Actor = attacker.Id, Target = region.Id,
        Text = defend ? $"{attacker.Name}向{region.Name}逼近，来敌参考战力 {Power(world, attacker)}。当地阵法 {region.Fort} 级，守军参考 {DefensePower(world, region)}。实际胜负依门人境界、阵法与对局裁定；选定守备后将进入数年战役，或撤离保存薪火。" : $"目标{(region.Owner == "" ? "尚无宗门控制" : "由" + world.Sects.Single(s => s.Id == region.Owner).Name + "控制")}，守军参考 {DefensePower(world, region)}，护阵 {region.Fort} 级。备军需130灵石、20灵材，围攻至少{MinimumBattleMonths(world, region)}月，另逐月供军。境界压制决定实战，总战力不能等价。主力归山前不能再次远征。",
        Choices = defend ?
        [
            new() { Id = "balanced", Label = "结阵固守", Description = "依托护山阵迎战，常规伤亡，保留反击机会。" },
            new() { Id = "sacrifice", Label = "燃灵护山", Description = "额外消耗 150 灵石与 40 灵材，防守战力提高 45%。", Stones = 150, Herbs = 40 },
            new() { Id = "retreat", Label = "撤离此地", Description = "放弃领地，保存弟子；若是最后山门，将护送道统流亡。" }
        ] :
        [
            new() { Id = "balanced", Label = "稳阵推进", Description = "标准战力与伤亡；最稳妥的常规战术。", Stones = 130, Herbs = 20 },
            new() { Id = "fierce", Label = "破釜强攻", Description = "进攻战力提高 25%，但双方伤亡提高；失败风险自负。", Stones = 130, Herbs = 20 },
            new() { Id = "careful", Label = "步步为营", Description = "进攻战力降低 10%，本宗伤亡显著减少。", Stones = 130, Herbs = 20 },
            new() { Id = "cancel", Label = "暂缓出征", Description = "不消耗宗令与资源，回到山门重新筹划。" }
        ]
    };

    private static void Battle(GameState world, Sect attacker, Region region, string tactic, double defenseBonus = 1, bool paid = false, int? campaignCause = null, double marchModifier = 1, IReadOnlyList<int>? deployed = null)
    {
        var defender = world.Sects.SingleOrDefault(s => s.Id == region.Owner);
        if (!paid) { if (attacker.Stones < 130 || attacker.Herbs < 20) return; Spend(attacker, 130, 20); }
        var heldBefore = world.Relics.Where(r => r.Scope == "personal" && r.HolderId is not null).ToList();
        var lostBefore = world.Relics.Where(r => r.Scope == "personal" && r.HolderId is null && r.Owner == "" && r.RegionId == region.Id).OrderBy(r => r.Id).ToList();
        var report = ResolveCultivatorBattle(world, attacker, region, tactic, defenseBonus, marchModifier, deployed);
        var victory = report.AttackerWon;
        var attackerLosses = report.AttackerDeaths;
        var defenderLosses = report.DefenderDeaths;
        var oldOwner = region.Owner;
        var causal = campaignCause ?? world.History.LastOrDefault(e => e.Sects.Contains(attacker.Id) && e.Kind is "war" or "cultivation")?.Id;
        if (victory)
        {
            region.Owner = attacker.Id; region.Fort = Math.Max(1, region.Fort - 1);
            attacker.Renown += 12; attacker.Stability = Math.Min(100, attacker.Stability + 5);
            if (defender is not null)
            {
                defender.Stability = Math.Max(0, defender.Stability - 15);
                var booty = Math.Min(defender.Stones, 120); defender.Stones -= booty; attacker.Stones += booty;
                ChangeRelation(attacker, defender, -35);
            }
        }
        else attacker.Stability = Math.Max(0, attacker.Stability - 7);
        var id = Log(world, "war", victory ? $"{attacker.Name}夺下{region.Name}" : $"{region.Name}守住了", $"门人逐一交锋{report.Rounds}轮；护山阵倍率{report.FormationBonus:F2}，低一大境界伤害仅为12%，每差一境递乘。军阵、战术、法宝、情报、战疲与粮道共同影响战斗。进攻方陨落{attackerLosses}人，守方陨落{defenderLosses}人；详细对局已收入战报。", defender is null ? [attacker.Id] : [attacker.Id, defender.Id], victory ? $"控制权 {oldOwner} → {attacker.Id}" : "控制权未改变", causal is null ? [] : [causal.Value]);
        report.EventId = id; world.BattleReports.Add(report);
        var battlefieldOwner = victory ? attacker.Id : defender?.Id ?? "";
        foreach (var relic in heldBefore.Where(r => r.HolderId is int holder && !world.People.Single(p => p.Id == holder).Alive).OrderBy(r => r.Id))
            MoveRelic(world, relic, battlefieldOwner, null, region.Id, battlefieldOwner == "" ? "战场遗失" : "战场遗物", id);
        if (victory)
            foreach (var relic in lostBefore) MoveRelic(world, relic, attacker.Id, null, region.Id, "战场重获", id);
        if (defender is not null)
            foreach (var person in Members(world, defender).Where(p => !IsAway(world, p.Id)))
                if (HeldRelic(world, person.Id) is { } relic) relic.RegionId = region.Id;
        EnsureLeader(world, attacker);
        if (defender is not null)
        {
            if (victory && Lands(world, defender).Count == 0) LoseSect(world, defender, attacker, id);
            else
            {
                EnsureLeader(world, defender);
                if (!defender.Eliminated && defender.Capital == region.Id && victory)
                {
                    defender.Capital = Lands(world, defender).OrderByDescending(r => r.Fort).FirstOrDefault()?.Id ?? "";
                    foreach (var relic in world.Relics.Where(r => r.Scope == "personal" && r.Owner == defender.Id && r.HolderId is null)) relic.RegionId = defender.Capital;
                }
            }
        }
        foreach (var sect in new[] { attacker, defender }.Where(s => s is not null && !s.Eliminated && !s.Exiled && s.Capital != ""))
            foreach (var relic in world.Relics.Where(r => r.Scope == "personal" && r.Owner == sect!.Id && r.HolderId is null)) relic.RegionId = sect!.Capital;
        CheckEnd(world);
    }

    private static int Casualties(GameState world, Sect sect, double rate)
    {
        var people = Members(world, sect);
        var preserve = sect.Id == world.PlayerId ? Math.Min(3, people.Count) : 0;
        var maxDeaths = Math.Max(0, people.Count - preserve);
        var count = 0;
        foreach (var person in people.OrderBy(p => p.Realm).ThenBy(p => p.Id))
        {
            if (world.Random() < rate && count < maxDeaths)
            {
                person.Alive = false; count++;
                person.Biography = $"青岚历 {world.Month / 12 + 1} 年，于宗门征战中陨落。";
            }
            else person.Injury = Math.Min(90, person.Injury + (int)(rate * 110) + world.Roll(3, 12));
        }
        return count;
    }

    private static void LoseSect(GameState world, Sect sect, Sect winner, int cause)
    {
        if (sect.Id == world.PlayerId)
        {
            sect.Exiled = true; sect.Capital = ""; sect.ExileSince = world.Month; world.Rebirths++;
            // A world with no free region requires a 350-stone negotiated foothold.
            // Preserve that emergency fund so any ember choice remains playable.
            sect.Stones = Math.Max(world.Regions.All(r => r.Owner != "") ? 350 : 200, sect.Stones / 2); sect.Herbs = Math.Max(80, sect.Herbs / 2); sect.Stability = Math.Max(35, sect.Stability - 20);
            var survivors = Members(world, sect).OrderByDescending(p => p.Realm).ThenByDescending(p => p.Potential).Take(6).Select(p => p.Id).ToHashSet();
            foreach (var p in Members(world, sect).Where(p => !survivors.Contains(p.Id)))
            {
                var relic = HeldRelic(world, p.Id);
                if (relic is not null) MoveRelic(world, relic, "", p.Id, relic.RegionId, "携宝离宗", cause);
                p.SectId = ""; p.Role = "散修";
            }
            EnsureLeader(world, sect);
            var id = Log(world, "exile", "山门虽毁，道统未绝", $"{winner.Name}占领最后山门，{survivors.Count} 名门徒携道藏与余资逃出生天。{sect.Name}仍能重建。", [sect.Id, winner.Id], "进入流亡；道藏、军阵与法宝保留", [cause]);
            world.PendingEvent = new GameEvent
            {
                Id = world.NewId(), Type = "ember", Title = "山河破碎，薪火未熄", Eyebrow = "道统续存 · 流亡篇", Actor = winner.Id,
                Text = $"护山阵崩塌前，幸存门徒已带出宗门印信、传承与法宝。前事并非一场空：道藏 {sect.Doctrine} 阶、军阵 {sect.Training} 级均保留。选择今后如何活下去，然后在地图上择地重建。历史事件 #{id} 将永远记住此夜。",
                Choices =
                [
                    new() { Id = "hidden", Label = "隐入深山", Description = "避开追兵，伤势大幅恢复，安定 +20。" },
                    new() { Id = "wander", Label = "化整为零，聚拢遗财", Description = "收回暗藏财物：灵石 +300，灵材 +100。" },
                    new() { Id = "revenge", Label = "血誓复宗", Description = "军阵等级 +1，全体幸存弟子修为 +35；仇恨不消。" }
                ]
            };
        }
        else
        {
            foreach (var relic in world.Relics.Where(r => r.Owner == sect.Id && r.Scope == "sect")) relic.Owner = winner.Id;
            foreach (var relic in world.Relics.Where(r => r.Owner == sect.Id && r.Scope == "personal" && r.HolderId is null).OrderBy(r => r.Id).ToList())
                MoveRelic(world, relic, winner.Id, null, sect.Capital, "宗库缴获", cause);
            Dissolve(world, sect, cause);
        }
    }

    private static void Dissolve(GameState world, Sect sect, int? cause = null)
    {
        foreach (var relic in world.Relics.Where(r => r.Scope == "personal" && r.Owner == sect.Id).OrderBy(r => r.Id).ToList())
        {
            var location = relic.HolderId is int holder && IsAway(world, holder) ? relic.RegionId : sect.Capital;
            MoveRelic(world, relic, "", relic.HolderId, location, relic.HolderId is null ? "宗库遗失" : "携宝离宗", cause ?? 0);
        }
        sect.Eliminated = true; sect.Exiled = false; sect.Capital = "";
        foreach (var r in Lands(world, sect)) r.Owner = "";
        foreach (var p in Members(world, sect)) { p.SectId = ""; p.Role = "散修"; p.Biography = "旧宗覆灭，余生暂作散修。"; }
        Log(world, "extinction", $"{sect.Name}道统断绝", "门人四散，山门不存。幸存者成为散修，原宗门不再作为独立势力存续。", [sect.Id], "宗门退出争霸", cause is null ? [] : [cause.Value]);
    }

    private static void Settle(GameState world, Sect sect, string target)
    {
        if (!sect.Exiled) throw new GameRuleException("只有流亡中的宗门可以重建。");
        var region = world.Regions.SingleOrDefault(r => r.Id == target) ?? throw new GameRuleException("请选择落脚地域。");
        var owner = world.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var noFreeLand = world.Regions.All(r => r.Owner != "");
        if (owner is not null && (!noFreeLand || owner.Capital == region.Id)) throw new GameRuleException("优先选择无主地域；若天下已无空地，可向他宗赎买非首府地域。");
        var cost = owner is null ? 150 : 350;
        Spend(sect, cost);
        if (owner is not null) { owner.Stones += cost; ChangeRelation(sect, owner, 10); }
        region.Owner = sect.Id; region.Fort = 1; sect.Capital = region.Id; sect.Exiled = false;
        foreach (var relic in world.Relics.Where(r => r.Scope == "personal" && r.Owner == sect.Id && (r.HolderId is null || !IsAway(world, r.HolderId.Value)))) relic.RegionId = region.Id;
        foreach (var p in Members(world, sect)) p.Injury = Math.Max(0, p.Injury - 30);
        Log(world, "rebirth", "故旗再起", $"{sect.Name}在{region.Name}重立山门。旧仇未雪，道统已续；这不是重新开始，而是同一个世界里的第二次崛起。", [sect.Id], $"灵石 -{cost}；恢复领地");
    }

    private static void CheckEnd(GameState world)
    {
        if (world.Status != "playing") return;
        if (world.Sects.Where(s => s.Id != world.PlayerId).All(s => s.Eliminated))
        {
            world.Status = "victory"; world.PendingEvent = null;
            Log(world, "victory", "万宗归一，山河共主", $"历经 {world.Month / 12} 年 {world.Month % 12} 月、{world.Rebirths} 次山门重建，{world.Player.Name}成为青岚域唯一存续的宗门。", [world.PlayerId]);
        }
    }
}
