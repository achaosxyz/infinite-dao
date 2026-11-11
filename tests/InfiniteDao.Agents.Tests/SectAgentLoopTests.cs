using System.Net;
using System.Text.Json;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Agents.Providers;
using InfiniteDao.Agents.Runtime;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Agents.Tests;

[TestClass]
public sealed class SectAgentLoopTests
{
    [TestMethod]
    public async Task NativeToolLoopRelaysResultsButNeverPersistsThinking()
    {
        var (world, memory) = Fixture();
        var query = new { type = "message", role = "assistant", stop_reason = "tool_use", content = new object[] {
            new { type = "thinking", thinking = "PRIVATE_THINKING_MUST_NOT_BE_SAVED", signature = "opaque-signature" },
            new { type = "tool_use", id = "query", name = "get_situation", input = new { section = "history" } } }, usage = new { input_tokens = 100, output_tokens = 20 } };
        var handler = new ScriptedHandler([query, Submit()]);
        await Run(world, memory, handler);
        Assert.AreEqual("稳固道统", memory.Plan!.Goal); Assert.HasCount(2, memory.Conversation);
        Assert.HasCount(2, handler.GenerationBodies); Assert.AreEqual(2, handler.CountChecks);
        var continuation = handler.GenerationBodies[1].GetRawText();
        StringAssert.Contains(continuation, "tool_result"); StringAssert.Contains(continuation, "opaque-signature");
        Assert.IsFalse(JsonSerializer.Serialize(memory).Contains("PRIVATE_THINKING_MUST_NOT_BE_SAVED", StringComparison.Ordinal));
        Assert.IsFalse(JsonSerializer.Serialize(memory).Contains("opaque-signature", StringComparison.Ordinal));
        Assert.AreEqual("", SectAgentSchedule.WakeReason(world, world.Sects.Single(s => s.Id == memory.SectId), memory));
    }

    [TestMethod]
    public async Task ExactNinetyPercentThresholdCompactsAndArchivesPublicConversation()
    {
        var (world, memory) = Fixture();
        memory.Conversation.Add(new() { Role = "user", Text = "旧约：北境盟约仍有效。", Month = 0 });
        memory.Conversation.Add(new() { Role = "assistant", Text = "原计划未完成，先积累军需。", Month = 0 });
        var handler = new ScriptedHandler([Tool("submit_summary", new { summary = "北境盟约有效，原计划未完成。" }), Submit()], [230400 - 4096, 100, 100]);
        await Run(world, memory, handler);
        Assert.AreEqual(1, memory.Compactions); Assert.HasCount(3, memory.Archive); Assert.HasCount(2, memory.Conversation);
        Assert.AreEqual("北境盟约有效，原计划未完成。", memory.Summary);
        Assert.HasCount(2, handler.GenerationBodies); Assert.AreEqual(3, handler.CountChecks);
        var summaryTool = handler.GenerationBodies[0].GetProperty("tools")[0];
        Assert.AreEqual("submit_summary", summaryTool.GetProperty("name").GetString()); Assert.IsTrue(summaryTool.GetProperty("strict").GetBoolean());
        StringAssert.Contains(handler.GenerationBodies[1].GetProperty("messages")[0].GetProperty("content").GetString()!, "北境盟约有效");
    }

    [TestMethod]
    public async Task BelowNinetyPercentDoesNotCompact()
    {
        var (world, memory) = Fixture(); var handler = new ScriptedHandler([Submit()], [230400 - 4096 - 1]);
        await Run(world, memory, handler);
        Assert.AreEqual(0, memory.Compactions); Assert.HasCount(0, memory.Archive); Assert.HasCount(1, handler.GenerationBodies);
    }

    [TestMethod]
    public async Task InvalidPlanIsReturnedAsToolFeedbackAndCanBeCorrected()
    {
        var (world, memory) = Fixture(); var invalid = Plan(); invalid.Target = "missing-region";
        var handler = new ScriptedHandler([Submit(invalid), Submit()]);
        await Run(world, memory, handler);
        var results = handler.GenerationBodies[1].GetProperty("messages");
        Assert.IsTrue(results[results.GetArrayLength() - 1].GetProperty("content")[0].GetProperty("is_error").GetBoolean());
        Assert.AreEqual("", memory.Plan!.Target); Assert.HasCount(2, handler.GenerationBodies);
    }

    [TestMethod]
    public async Task UnknownToolsCannotExecuteAndReceiveErrorFeedback()
    {
        var (world, memory) = Fixture(); var handler = new ScriptedHandler([Tool("run_shell", new { command = "must-not-run" }), Submit()]);
        await Run(world, memory, handler);
        var messages = handler.GenerationBodies[1].GetProperty("messages");
        var result = messages[messages.GetArrayLength() - 1].GetProperty("content")[0];
        Assert.IsTrue(result.GetProperty("is_error").GetBoolean()); StringAssert.Contains(result.GetProperty("content").GetString()!, "不能调用其他工具");
    }

    [TestMethod]
    public async Task CountEndpointFailurePausesWithoutGeneratingOrDiscardingMemory()
    {
        var (world, memory) = Fixture(); memory.Conversation.Add(new() { Text = "保留旧记忆" });
        var handler = new ScriptedHandler([]) { RejectCount = true };
        var exception = await Assert.ThrowsAsync<ModelDecisionException>(() => Run(world, memory, handler));
        StringAssert.Contains(exception.Message, "上下文计数接口"); Assert.HasCount(0, handler.GenerationBodies);
        Assert.HasCount(1, memory.Conversation); Assert.IsNull(memory.Plan); Assert.AreEqual(1, handler.CountChecks);
    }

    [TestMethod]
    public async Task CompactFailureDoesNotDiscardPreviousPublicHistory()
    {
        var (world, memory) = Fixture(); memory.Summary = "保留摘要"; memory.Conversation.Add(new() { Text = "保留原会话" });
        var handler = new ScriptedHandler([Tool("submit_summary", new { wrong = "invalid" })], [230400 - 4096, 100]);
        await Assert.ThrowsAsync<ModelDecisionException>(() => Run(world, memory, handler));
        Assert.AreEqual("保留摘要", memory.Summary); Assert.HasCount(1, memory.Conversation); Assert.HasCount(0, memory.Archive);
    }

    [TestMethod]
    public async Task CompactRejectsOrdinaryTextEvenWhenItContainsValidJson()
    {
        var (world, memory) = Fixture(); memory.Conversation.Add(new() { Text = "原始公开记录" });
        var handler = new ScriptedHandler([Text(new { summary = "不能从文本提取" })], [230400 - 4096, 100]);
        var exception = await Assert.ThrowsAsync<ModelDecisionException>(() => Run(world, memory, handler));
        StringAssert.Contains(exception.Message, "submit_summary"); Assert.HasCount(1, memory.Conversation); Assert.HasCount(0, memory.Archive);
    }

    [TestMethod]
    public async Task ReviewUsesPreviousConversationAndFreshObservation()
    {
        var (world, memory) = Fixture(); var handler = new ScriptedHandler([Submit(), Submit()]);
        await Run(world, memory, handler); world.Month = 60; world.Sects.Single(s => s.Id == memory.SectId).Stones = 98765;
        await Run(world, memory, handler);
        var messages = handler.GenerationBodies[1].GetProperty("messages");
        Assert.IsTrue(messages.EnumerateArray().Any(m => m.GetProperty("role").GetString() == "assistant"));
        StringAssert.Contains(messages[messages.GetArrayLength() - 1].GetProperty("content").GetString()!, "98765");
        Assert.AreEqual(60, memory.LastReviewMonth); Assert.HasCount(4, memory.Conversation);
    }

    [TestMethod]
    public async Task StepLimitStopsAnUnproductiveLoopInsteadOfFallingBack()
    {
        var (world, memory) = Fixture(); var handler = new ScriptedHandler(Enumerable.Range(0, 8).Select(_ => Tool("get_situation", new { section = "history" })));
        var exception = await Assert.ThrowsAsync<ModelDecisionException>(() => Run(world, memory, handler));
        StringAssert.Contains(exception.Message, "步骤上限"); Assert.HasCount(8, handler.GenerationBodies); Assert.IsNull(memory.Plan);
    }

    [TestMethod]
    public async Task OnlyMeaningfulChangesWakeAStablePlan()
    {
        var (world, memory) = Fixture(); await Run(world, memory, new ScriptedHandler([Submit()]));
        var sect = world.Sects.Single(s => s.Id == memory.SectId);
        world.Month = 1; Assert.AreEqual("", SectAgentSchedule.WakeReason(world, sect, memory));
        sect.Stones = 0; Assert.AreEqual("", SectAgentSchedule.WakeReason(world, sect, memory), "财政噪声受六个月合并窗口控制。");
        world.Month = 6; Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
        sect.Stones = 950; world.Month = 1;
        GameEngine.Log(world, "war", "战役已结算", "本宗受到实际战争影响。", [sect.Id]);
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
        memory.LastObservedEventId = world.History.Max(e => e.Id);
        sect.LeaderId = GameEngine.Members(world, sect).First(p => p.Id != sect.LeaderId).Id;
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
        memory.LastSituation = SectAgentSchedule.Situation(world, sect);
        world.Month = memory.NextReviewMonth;
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
    }

    [TestMethod]
    public async Task TargetOwnershipChangesWakeTheAgentImmediately()
    {
        var (world, memory) = Fixture(); var sect = world.Sects.Single(s => s.Id == memory.SectId);
        var target = world.Regions.First(r => r.Owner != sect.Id && GameEngine.Border(world, sect, r));
        var plan = Plan(); plan.Target = target.Id; await Run(world, memory, new ScriptedHandler([Submit(plan)]));
        target.Owner = "tiangong";
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
        target.Owner = sect.Id;
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
    }

    [TestMethod]
    public async Task CampaignStartDoesNotReplanButSupplyFailureDoes()
    {
        var (world, memory) = Fixture(); await Run(world, memory, new ScriptedHandler([Submit()]));
        var sect = world.Sects.Single(s => s.Id == memory.SectId); world.Month = 1;
        var origin = GameEngine.Log(world, "campaign", "计划中的出征", "执事开始执行已议定计划。", [sect.Id]);
        Assert.AreEqual("", SectAgentSchedule.WakeReason(world, sect, memory));
        GameEngine.Log(world, "campaign", "断供撤军", "原计划执行失败，需要重新议政。", [sect.Id], "控制权未变", [origin]);
        Assert.IsFalse(string.IsNullOrEmpty(SectAgentSchedule.WakeReason(world, sect, memory)));
    }

    [TestMethod]
    public async Task MalformedToolEnvelopeBecomesAPausedModelError()
    {
        var (world, memory) = Fixture();
        var malformed = new { type = "message", role = "assistant", stop_reason = "tool_use", content = new[] { new { type = "tool_use", name = "submit_strategy", input = Plan() } } };
        var handler = new ScriptedHandler([malformed]);
        await Assert.ThrowsAsync<ModelDecisionException>(() => Run(world, memory, handler));
        Assert.IsNull(memory.Plan); Assert.HasCount(0, memory.Conversation);
    }

    [TestMethod]
    public void DefaultContextIsTwoHundredFiftySixThousandWithNinetyPercentCompaction()
    {
        var options = new LlmRuntimeConfiguration();
        Assert.AreEqual(256000, options.ContextWindowTokens); Assert.AreEqual(.9, options.CompactionThreshold);
        Assert.AreEqual(230400d, options.ContextWindowTokens * options.CompactionThreshold);
        var config = new LlmConfiguration { Endpoint = "https://provider.example/v1/messages", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "", Model = "test", Request = new LlmRequestConfiguration { MaxOutputTokens = 256000 } };
        Assert.IsTrue(config.Validate(_ => null).Any(e => e.Contains("compaction threshold", StringComparison.Ordinal)));
    }

    private static (GameState, SectAgentMemory) Fixture()
    {
        var world = WorldFactory.Create("qingyun", 122, "rules", "normal", compact: true);
        return (world, new SectAgentMemory { SectId = "chixia", LastTrigger = "首次议政" });
    }

    private static async Task Run(GameState world, SectAgentMemory memory, ScriptedHandler handler)
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        var config = new LlmConfiguration { Endpoint = "https://provider.example/v1/messages", Model = "test", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "" };
        await new SectAgentLoop(client).RunAsync(world, world.Sects.Single(s => s.Id == memory.SectId), memory, config, _ => Task.CompletedTask, (_, _) => Task.CompletedTask, CancellationToken.None);
    }

    private static SectAgentPlan Plan() => new() { Policy = "balanced", Goal = "稳固道统", Reason = "先保供养，再积累突破与战争所需。", Stages = ["保障供养", "培养核心战力"], ReviewAfterMonths = 60, ReconsiderWhen = "遭遇战争或核心修士突破" };
    private static object Submit(SectAgentPlan? plan = null) => Tool("submit_strategy", plan ?? Plan());
    private static object Tool(string name, object input) => new { type = "message", role = "assistant", stop_reason = "tool_use", content = new[] { new { type = "tool_use", id = "tool", name, input } }, usage = new { input_tokens = 100, output_tokens = 20 } };
    private static object Text(object data) => new { type = "message", role = "assistant", stop_reason = "end_turn", content = new[] { new { type = "text", text = JsonSerializer.Serialize(data) } }, usage = new { input_tokens = 100, output_tokens = 20 } };

    private sealed class ScriptedHandler(IEnumerable<object> replies, IEnumerable<long>? tokenCounts = null) : HttpMessageHandler
    {
        private readonly Queue<object> replies = new(replies);
        private readonly Queue<long> tokenCounts = new(tokenCounts ?? []);
        public List<JsonElement> GenerationBodies { get; } = [];
        public int CountChecks { get; private set; }
        public bool RejectCount { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (request.RequestUri!.AbsolutePath.EndsWith("/count_tokens", StringComparison.Ordinal))
            {
                CountChecks++;
                if (RejectCount) return new(HttpStatusCode.NotFound) { Content = new StringContent("private-provider-error") };
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { input_tokens = tokenCounts.Count == 0 ? 100 : tokenCounts.Dequeue() })) };
            }
            GenerationBodies.Add(body.RootElement.Clone());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(replies.Dequeue())) };
        }
    }
}
