namespace InfiniteDao.Core.Game;

public sealed class SectAgentMemory
{
    public string SectId { get; set; } = "";
    public SectAgentPlan? Plan { get; set; }
    public string Summary { get; set; } = "";
    public List<AgentConversationEntry> Conversation { get; set; } = [];
    public List<AgentConversationEntry> Archive { get; set; } = [];
    public int LastReviewMonth { get; set; } = -1;
    public int NextReviewMonth { get; set; }
    public int LastObservedEventId { get; set; }
    public string LastSituation { get; set; } = "";
    public string TargetOwner { get; set; } = "";
    public bool FinancialCrisis { get; set; }
    public long ContextTokens { get; set; }
    public int Compactions { get; set; }
    public string LastTrigger { get; set; } = "";
}

public sealed class SectAgentPlan
{
    public string Policy { get; set; } = "balanced";
    public string Target { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Reason { get; set; } = "";
    public List<string> Stages { get; set; } = [];
    public string ReconsiderWhen { get; set; } = "";
    public int ReviewAfterMonths { get; set; } = 60;
}

// Public game observations and model submissions only. Never store thinking blocks or credentials.
public sealed class AgentConversationEntry
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public int Month { get; set; }
}
