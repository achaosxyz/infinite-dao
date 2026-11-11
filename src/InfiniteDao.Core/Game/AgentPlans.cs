namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    public static void ValidateAgentPlan(GameState world, Sect sect, SectAgentPlan plan)
    {
        if (sect.Id == world.PlayerId || sect.Eliminated) throw new GameRuleException("只能为存续的其他宗门制定智能计划。");
        ValidatePlanShape(plan);
        if (plan.Target != "" && !world.Regions.Any(r => r.Id == plan.Target && r.Owner != sect.Id && Border(world, sect, r)))
            throw new GameRuleException("计划目标已占领或不可达，请重新观察边境。");
    }

    private static void ValidatePlanShape(SectAgentPlan plan)
    {
        if (!WorldFactory.Policies.Contains(plan.Policy) || plan.Reason.Length is < 1 or > 300 || plan.Goal.Length is < 1 or > 500
            || plan.Stages.Count is < 1 or > 8 || plan.Stages.Any(s => string.IsNullOrEmpty(s) || s.Length > 600)
            || plan.ReconsiderWhen.Length > 600 || plan.ReviewAfterMonths is < 12 or > 1200)
            throw new GameRuleException("宗门计划格式或复盘期限无效。");
    }
}
