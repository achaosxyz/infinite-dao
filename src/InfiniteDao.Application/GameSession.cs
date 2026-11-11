using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Core.Game;
using InfiniteDao.Infrastructure.Persistence;

namespace InfiniteDao.Application;

public sealed partial class GameSession(string root, HttpClient client, string? saveDirectory = null)
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public GameSaveStore Saves { get; } = new(saveDirectory ?? Path.Combine(root, "artifacts", "game", "saves"));
    public GameState? State { get; set; }
    private int callsUsed;
    public int CallsUsed => Volatile.Read(ref callsUsed);
    public string Activity => Volatile.Read(ref activity);
    private long revision;
    public long NextRevision() => ++revision;

    public LlmConfiguration ReadConfiguration()
    {
        try
        {
            var json = File.ReadAllText(Path.Combine(root, "config", "llm.local.json"));
            var cfg = JsonSerializer.Deserialize<LlmConfiguration>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false }) ?? throw new JsonException();
            var errors = cfg.Validate(Environment.GetEnvironmentVariable);
            if (errors.Count > 0) throw new GameRuleException("LLM 配置未通过校验，请检查 config/llm.local.json；可先选择离线世界游玩。");
            return cfg;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { throw new GameRuleException("无法读取本地 LLM 配置；请检查配置文件，或明确选择离线世界。"); }
    }

    public ModelConnectionStatus ModelStatus()
    {
        try { var c = ReadConfiguration(); return new(true, c.Request!.ReasoningEffort, CallsUsed, c.Runtime!.MaxConcurrentRequests, c.Runtime.ContextWindowTokens, c.Runtime.CompactionThreshold, c.Runtime.StrategyReviewMonths); }
        catch (GameRuleException) { return new(false, "high", CallsUsed, 0); }
    }

    // Archived web endpoint compatibility: no quota is renewed or enforced.
    public void RenewBudget() { _ = ReadConfiguration(); }

    public GameState Require(long expectedRevision)
    {
        if (State is null) throw new GameRuleException("请先选择宗门，开始一局新游戏。");
        if (State.Revision != expectedRevision) throw new GameRuleException("世界已发生变化，请刷新状态后再作决策。");
        return State;
    }

    public async Task Commit(GameState state)
    {
        await SaveState(state);
        StopAgents();
    }

    private async Task SaveState(GameState state, bool advanceRevision = true)
    {
        GameEngine.Validate(state);
        if (advanceRevision) state.Revision = NextRevision();
        await Saves.SaveAsync(state, "auto"); State = state;
    }

    public async Task Advance(long expectedRevision, CancellationToken cancellationToken)
    {
        var live = Require(expectedRevision);
        if (live.PendingEvent is not null || live.Player.Exiled || live.Status != "playing") throw new GameRuleException("请先处理议事或重建山门，再推进时间。");
        var next = live.Copy();
        DrainBackground(next);
        var decisions = next.Mode == "llm" ? StandingDecisions(next) : null;
        GameEngine.AdvanceMonth(next, decisions);
        await SaveState(next);
        ScheduleBackground(next, cancellationToken);
    }

    public async Task NewGame(string sect, int seed, string mode, string difficulty, CancellationToken cancellationToken)
    {
        var next = WorldFactory.Create(sect, seed, mode, difficulty);
        StopAgents();
        await SaveState(next);
        ScheduleBackground(next, cancellationToken);
    }

    public async Task Execute(long expectedRevision, GameCommand command, CancellationToken cancellationToken)
    {
        var live = Require(expectedRevision); var next = live.Copy();
        DrainBackground(next);
        var previousMode = next.Mode;
        GameEngine.Command(next, command);
        if (previousMode != next.Mode)
        {
            StopAgents(); next.AgentStatus = "";
            if (next.Mode == "llm") foreach (var memory in next.AgentMemories) memory.NextReviewMonth = next.Month;
        }
        await SaveState(next);
        ScheduleBackground(next, cancellationToken);
    }

}

public sealed record ModelConnectionStatus(bool Configured, string? Effort, int CallsUsed, int MaxConcurrentRequests, int ContextWindowTokens = 256000, double CompactionThreshold = .9, int StrategyReviewMonths = 60);
