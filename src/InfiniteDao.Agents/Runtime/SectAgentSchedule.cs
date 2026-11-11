using InfiniteDao.Core.Game;

namespace InfiniteDao.Agents.Runtime;

public static class SectAgentSchedule
{
    public static string WakeReason(GameState world, Sect sect, SectAgentMemory memory)
    {
        if (memory.Plan is null) return "首次议政：建立长期计划";
        if (world.Month >= memory.NextReviewMonth) return "到期复盘";
        if (memory.Plan.Target != "" && !world.Regions.Any(r => r.Id == memory.Plan.Target && r.Owner != sect.Id && GameEngine.Border(world, sect, r)))
            return "计划目标已完成或不再可达";
        if (memory.Plan.Target != "" && world.Regions.Single(r => r.Id == memory.Plan.Target).Owner != memory.TargetOwner)
            return "目标控制权发生变化";
        if (memory.LastSituation != Situation(world, sect)) return "掌门、核心境界、领土、来袭战役或诸宗存亡发生变化";
        if (world.History.Any(e => e.Id > memory.LastObservedEventId && e.Sects.Contains(sect.Id)
            && (e.Kind is "war" or "exile" || e.Kind == "campaign" && e.Causes.Count > 0)))
            return "本宗战役已结算或山门失守";
        if (world.Month - memory.LastReviewMonth >= 6 && memory.FinancialCrisis != FinancialCrisis(world, sect))
            return "供养危机状态变化";
        return "";
    }

    public static string Situation(GameState world, Sect sect)
    {
        var people = GameEngine.Members(world, sect);
        var leader = people.SingleOrDefault(p => p.Id == sect.LeaderId);
        return $"{sect.LeaderId}:{leader?.Realm}:{people.Select(p => p.Realm).DefaultIfEmpty(-1).Max()}:{sect.Capital}:"
            + string.Join(',', GameEngine.Lands(world, sect).Select(r => r.Id).Order()) + ":"
            + string.Join(',', world.Campaigns.Where(c => c.Defender == sect.Id).Select(c => c.Id).Order()) + ":"
            + string.Join(',', sect.Truces.Where(t => t.Value > world.Month).Select(t => t.Key).Order()) + ":"
            + string.Join(',', world.Sects.Where(s => s.Eliminated).Select(s => s.Id).Order());
    }

    public static bool FinancialCrisis(GameState world, Sect sect) => sect.Stones < GameEngine.Upkeep(world, sect) * 3 || sect.Stability < 35;
}
