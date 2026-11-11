using InfiniteDao.Core.Game;

namespace InfiniteDao.Application;

public sealed partial class GameSession
{
    private bool DrainBackground(GameState world)
    {
        var changed = false;
        while (backgroundUpdates.TryDequeue(out var update))
        {
            if (update.Epoch != backgroundEpoch) continue;
            world.ModelRequests += update.Requests;
            world.ModelTokens += update.Tokens;
            world.ModelContextChecks += update.ContextChecks;
            changed |= update.Requests != 0 || update.Tokens != 0 || update.ContextChecks != 0;
            if (update.Finished && update.Job is { } finished)
            {
                if (backgroundJobs.TryGetValue(finished.Key, out var current) && ReferenceEquals(current, finished))
                    backgroundJobs.Remove(finished.Key);
                if (world.Mode == "llm" && world.AgentStatus.StartsWith(finished.Name, StringComparison.Ordinal))
                { world.AgentStatus = ""; changed = true; }
            }
            if (world.Mode != "llm" || update.Job is { } job && !Relevant(world, job)) continue;
            if (update.Failed)
            {
                world.Mode = "rules";
                world.AgentStatus = update.Status!;
                GameEngine.Log(world, "agent", "智能世界已自动切换离线", world.AgentStatus, [world.PlayerId]);
                foreach (var pending in backgroundJobs.Values) pending.Cancel();
                Volatile.Write(ref activity, world.AgentStatus);
                changed = true;
                continue;
            }
            if (update.Status is { } status)
            {
                world.AgentStatus = status;
                Volatile.Write(ref activity, status);
                changed = true;
            }
            if (update.Memory is { } memory)
            {
                var sect = world.Sects.Single(s => s.Id == memory.SectId);
                try
                {
                    GameEngine.ValidateAgentPlan(world, sect, memory.Plan!);
                    // Review intervals start at adoption, not before a slow model finished thinking.
                    memory.NextReviewMonth = world.Month + (memory.NextReviewMonth - memory.LastReviewMonth);
                }
                catch (GameRuleException)
                {
                    // Preserve the public submission, but never execute a now-unreachable target.
                    memory.Plan = null;
                    memory.NextReviewMonth = world.Month;
                    memory.Conversation.Add(new AgentConversationEntry { Month = world.Month, Role = "user", Text = "返回时世界已经变化，目标已完成或不可达；本计划未执行，请根据最新观察重新规划。" });
                    GameEngine.Log(world, "agent", "宗门计划因世界变化未采用", $"{sect.Name}的后台计划返回时目标已经失效，将重新观察；世界继续演进。", [sect.Id]);
                }
                var index = world.AgentMemories.FindIndex(m => m.SectId == memory.SectId);
                if (index >= 0) world.AgentMemories[index] = memory; else world.AgentMemories.Add(memory);
                if (update.Job is { } owner && world.AgentStatus.StartsWith(owner.Name, StringComparison.Ordinal)) world.AgentStatus = "";
                changed = true;
            }
        }
        Volatile.Write(ref activeBackgroundJobs, backgroundJobs.Count);
        return changed;
    }

    private static List<AgentDecision> StandingDecisions(GameState world)
    {
        return world.Sects.Where(s => s.Id != world.PlayerId && !s.Eliminated).OrderBy(s => s.Id).Select(sect =>
        {
            var memory = world.AgentMemories.SingleOrDefault(m => m.SectId == sect.Id);
            if (memory?.Plan is { } plan)
            {
                try
                {
                    GameEngine.ValidateAgentPlan(world, sect, plan);
                    return new AgentDecision { SectId = sect.Id, Month = memory.LastReviewMonth, Policy = plan.Policy, Target = plan.Target, Reason = plan.Reason, Source = "llm" };
                }
                catch (GameRuleException) { memory.NextReviewMonth = world.Month; }
            }
            var target = world.Regions.Any(r => r.Id == sect.Target && r.Owner != sect.Id && GameEngine.Border(world, sect, r)) ? sect.Target : "";
            return new AgentDecision { SectId = sect.Id, Month = world.Month, Policy = sect.Policy, Target = target,
                Reason = "后台Agent尚在规划，沿用宗门当前战略方向；日常事务由执事继续执行。", Source = "standing" };
        }).ToList();
    }
}
