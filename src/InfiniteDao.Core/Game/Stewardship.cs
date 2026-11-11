namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    public static bool IsAway(GameState world, int personId) => world.Campaigns.Any(c => c.Participants.Contains(personId))
        || world.Expeditions.Any(e => e.DiscipleId == personId || e.GuardianId == personId);

    private static void AssignDisciple(GameState world, GameCommand command)
    {
        var person = Members(world, world.Player).SingleOrDefault(p => p.Id.ToString() == command.Target)
            ?? throw new GameRuleException("只能安排本宗在世门人。");
        if (command.Type == "focus")
        {
            var enabled = command.Option == "on";
            if (command.Option is not ("on" or "off")) throw new GameRuleException("培养选项无效。");
            if (enabled && !person.Focused && Members(world, world.Player).Count(p => p.Focused) >= 3)
                throw new GameRuleException("宗门最多同时重点培养三人。");
            person.Focused = enabled;
            Log(world, "decision", enabled ? "立为道种" : "回归常例", $"{person.Name}{(enabled ? "列入重点培养，每月额外供给灵材，修炼加快；并非不会陨落。" : "恢复常规修炼供给。")}", [world.PlayerId]);
            return;
        }
        if (IsAway(world, person.Id)) throw new GameRuleException("门人在外，归山后才能更换护道人。");
        if (command.Option == "") { person.GuardianId = null; return; }
        var guardian = Members(world, world.Player).SingleOrDefault(p => p.Id.ToString() == command.Option)
            ?? throw new GameRuleException("护道人必须是本宗在世门人。");
        if (guardian.Id == person.Id || guardian.Realm <= person.Realm || guardian.Injury >= 30 || IsAway(world, guardian.Id))
            throw new GameRuleException("护道人须境界更高、伤势低于30，且当前未出征或历练。");
        if (guardian.GuardianId is not null || Members(world, world.Player).Any(p => p.GuardianId == guardian.Id && p.Id != person.Id)
            || Members(world, world.Player).Any(p => p.GuardianId == person.Id))
            throw new GameRuleException("每位护道人只能保护一名弟子，不能形成连环护道。");
        person.GuardianId = guardian.Id;
        Log(world, "decision", "护道之约", $"{guardian.Name}受托为{person.Name}护道；历练时须实际同行，护道人也可能受伤或陨落。", [world.PlayerId]);
    }

    private static void StewardDevelop(GameState world, Sect sect)
    {
        if (world.Month % 6 != 0) return;
        var lands = Lands(world, sect);
        if (lands.Count == 0) return;
        var members = Members(world, sect);
        var reserve = ReservedStones(world, sect);
        var capacity = Math.Min(100, 12 + lands.Count * 7);
        if (members.Count < capacity && sect.Stones > reserve + 140 && HerbIncome(world, sect) > members.Sum(p => 1 + p.Realm))
        {
            var count = Math.Min(4, capacity - members.Count);
            Spend(sect, 140);
            for (var i = 0; i < count; i++) NewApprentice(world, sect);
            lands[0].Population = Math.Max(0, lands[0].Population - count);
            Log(world, "people", "执事开山纳新", $"{sect.Name}依战略收录{count}名弟子，保留半年供养储备。", [sect.Id], $"弟子 +{count}；灵石 -140");
        }
        var develop = lands.Where(r => r.Development < 5).OrderBy(r => r.Development).ThenBy(r => r.Id).FirstOrDefault();
        if (develop is not null && sect.Stones > reserve + develop.Development * 100 + 80
            && (sect.Policy is "prosperity" or "balanced" || Income(world, sect) < Upkeep(world, sect) * 2))
        {
            Spend(sect, develop.Development * 100 + 80); develop.Development++;
            Log(world, "economy", "执事营建坊市", $"{develop.Name}发展至{develop.Development}级。", [sect.Id]);
        }
        if (sect.Policy is "cultivation" or "balanced" && sect.Doctrine < 8 && sect.Stones > reserve + 220 + sect.Doctrine * 80 && sect.Herbs > 180)
        { Spend(sect, 220 + sect.Doctrine * 80, 60); sect.Doctrine++; }
        else if (sect.Training < 10 && sect.Stones > reserve + 160 + sect.Training * 60 && sect.Herbs > 160)
        { Spend(sect, 160 + sect.Training * 60, 30); sect.Training++; }
        var fort = lands.Where(r => r.Fort < 5 && r.Neighbors.Any(n => world.Regions.Any(x => x.Id == n && x.Owner != sect.Id)))
            .OrderBy(r => r.Fort).FirstOrDefault();
        if (fort is not null && sect.Stones > reserve + fort.Fort * 100 + 60 && sect.Herbs > 100)
        { Spend(sect, fort.Fort * 100 + 60, 15); fort.Fort++; }
        if (members.Any(p => p.Injury >= 40) && sect.Stones > reserve + 50 && sect.Herbs > 150)
        { Spend(sect, 50, 50); foreach (var p in members.Where(p => !IsAway(world, p.Id))) p.Injury = Math.Max(0, p.Injury - 30); }
        if (sect.Policy == "diplomacy")
        {
            var ally = world.Sects.Where(s => s.Id != sect.Id && !s.Eliminated).OrderByDescending(s => sect.Relations.GetValueOrDefault(s.Id)).FirstOrDefault();
            if (ally is not null && sect.Relations.GetValueOrDefault(ally.Id) >= 15 && !AtPeace(world, sect, ally) && sect.Stones > reserve + 180)
            { Spend(sect, 180); sect.Truces[ally.Id] = ally.Truces[sect.Id] = world.Month + 60; }
        }
    }

    private static void NewApprentice(GameState world, Sect sect)
    {
        var p = WorldFactory.NewCultivator(world, sect.Id);
        p.NextJourneyMonth = world.Month + world.Roll(18, 36);
    }

    private static void AdvanceExpeditions(GameState world)
    {
        foreach (var journey in world.Expeditions.OrderBy(e => e.Id).ToList())
        {
            var person = world.People.Single(p => p.Id == journey.DiscipleId);
            var sect = world.Sects.Single(s => s.Id == journey.SectId);
            if (!person.Alive || sect.Eliminated || sect.Exiled || person.SectId != sect.Id)
            { world.Expeditions.Remove(journey); continue; }
            if (--journey.MonthsRemaining > 0) continue;
            var region = world.Regions.Single(r => r.Id == journey.RegionId);
            var guardian = world.People.SingleOrDefault(p => p.Id == journey.GuardianId && p.Alive && p.SectId == sect.Id);
            var danger = Math.Clamp((region.Spirit - person.Realm - 1) * .035 + (region.Owner != "" && region.Owner != sect.Id ? .08 : .02), .02, .3);
            if (guardian is not null) danger *= Math.Pow(.25, guardian.Realm - person.Realm);
            var roll = world.Random();
            if (roll < danger)
            {
                person.Alive = false; person.Biography += $" 青岚历{world.Month / 12 + 1}年于{region.Name}历练遇险陨落。";
                Log(world, "death", $"{person.Name}历练陨落", $"{person.Name}未能从{region.Name}归来。{(guardian is null ? "此行没有护道人同行。" : guardian.Name + "奋力护持仍未救回，身负重伤。")}", [sect.Id], "门徒 -1", [journey.OriginEvent]);
                if (guardian is not null) { guardian.Injury = Math.Min(95, guardian.Injury + 45); if (world.Random() < .08) { guardian.Alive = false; guardian.Biography += " 护道时殉身。"; Log(world, "death", "护道者殉身", $"{guardian.Name}于{region.Name}护道陨落。", [sect.Id], "门徒 -1", [journey.OriginEvent]); } }
            }
            else
            {
                var injured = roll < danger + .17;
                person.Injury = Math.Min(95, person.Injury + (injured ? world.Roll(20, 50) : 0));
                person.Progress = Math.Min(120, person.Progress + world.Roll(8, 20));
                var stones = world.Roll(30, 100) * (person.Realm + 1); sect.Stones += stones; sect.Herbs += world.Roll(5, 20);
                Log(world, "journey", $"{person.Name}历练归山", $"自{region.Name}归来，{(injured ? "带伤而归，需静养" : "心境精进")}。{(guardian is null ? "独自历练" : guardian.Name + "随行护道") }。", [sect.Id], $"灵石 +{stones}；修为增长", [journey.OriginEvent]);
            }
            world.Expeditions.Remove(journey); person.NextJourneyMonth = world.Month + world.Roll(24, 60);
            EnsureLeader(world, sect);
        }
        foreach (var sect in world.Sects.Where(s => !s.Eliminated && !s.Exiled).OrderBy(s => s.Id))
        {
            var members = Members(world, sect);
            foreach (var p in members.Where(p => p.GuardianId is not null && !members.Any(g => g.Id == p.GuardianId && g.Realm > p.Realm))) p.GuardianId = null;
            var slots = Math.Max(0, Math.Min(4, members.Count / 5) - world.Expeditions.Count(e => e.SectId == sect.Id));
            foreach (var p in members.Where(p => p.Role != "掌门" && p.Injury < 20 && p.NextJourneyMonth <= world.Month
                && !IsAway(world, p.Id) && !members.Any(d => d.GuardianId == p.Id)).OrderBy(p => p.NextJourneyMonth).ThenBy(p => p.Id).Take(slots))
            {
                var guardian = members.SingleOrDefault(g => g.Id == p.GuardianId && g.Realm > p.Realm && g.Injury < 30 && !IsAway(world, g.Id));
                if (p.GuardianId is not null && guardian is null) continue;
                var destinations = world.Regions.Where(r => r.Owner == sect.Id || Border(world, sect, r)).OrderBy(r => r.Id).ToList();
                if (destinations.Count == 0) continue;
                var region = destinations[world.Roll(0, destinations.Count - 1)];
                var cause = Log(world, "journey", $"{p.Name}下山历练", $"前往{region.Name}，预计一至三年归来。{(guardian is null ? "无护道人随行，遇险可能陨落。" : guardian.Name + "同行护道。")}", [sect.Id]);
                world.Expeditions.Add(new Expedition { Id = world.NewId(), DiscipleId = p.Id, GuardianId = guardian?.Id, SectId = sect.Id, RegionId = region.Id, MonthsRemaining = world.Roll(12, 36), OriginEvent = cause });
            }
        }
    }
}
