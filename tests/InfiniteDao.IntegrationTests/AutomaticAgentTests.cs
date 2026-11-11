using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Application;
using InfiniteDao.Core.Game;

namespace InfiniteDao.IntegrationTests;

[TestClass]
public sealed class AutomaticAgentTests
{
    [TestMethod]
    public async Task SlowAgentsDoNotBlockCommandsOrMonthlyProgressAndPumpDoesNotReviseWorld()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(blockAllResponses: true);
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root, maxConcurrentRequests: 2);
            await session.NewGame("qingyun", 321, "llm", "normal", CancellationToken.None);
            await handler.FirstModelRequest.WaitAsync(TimeSpan.FromSeconds(5));
            await PumpUntilAsync(session, () => handler.RequestsFor("baicao") > 0, TimeSpan.FromSeconds(5));

            Assert.IsTrue(session.HasBackgroundWork);
            var revision = session.State!.Revision;
            await PumpAsync(session);
            Assert.AreEqual(revision, session.State.Revision, "Pump 只收后台结果，不增加世界 Revision。");

            await session.Execute(session.State.Revision, new GameCommand("choose", Option: "cultivation"), CancellationToken.None);
            await session.Advance(session.State.Revision, CancellationToken.None);
            await session.Advance(session.State.Revision, CancellationToken.None);

            Assert.AreEqual(2, session.State.Month, "即使所有模型响应仍被挂起，月结也应继续。");
            Assert.AreEqual(revision + 3, session.State.Revision, "玩家命令和两次月结各自正常增加 Revision。");
            Assert.IsTrue(session.HasBackgroundWork);

            handler.ReleaseBlockedResponses();
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(20));
            var slowPlan = session.State!.AgentMemories.Single(m => m.SectId == "baicao");
            Assert.AreEqual(0, slowPlan.LastReviewMonth, "慢模型计划保留其观察月份。");
            Assert.AreEqual(62, slowPlan.NextReviewMonth, "计划采纳月应按原复盘间隔重新计算到期月。");
            Assert.AreEqual(revision + 3, session.State.Revision);
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task PendingCouncilStaysRuleDefinedWhileSectAgentsPlan()
    {
        var root = NewDirectory();
        var handler = new FlowHandler();
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            await session.NewGame("qingyun", 419, "llm", "normal", CancellationToken.None);
            var original = JsonSerializer.Serialize(session.State!.PendingEvent);
            var revision = session.State.Revision;
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, handler.CouncilCalls, "议事不得发起纯叙事模型请求。");
            Assert.IsGreaterThan(0, handler.Calls, "敌宗策略 Agent 仍需正常工作。");
            Assert.HasCount(session.State!.Sects.Count - 1, session.State.AgentMemories);
            Assert.IsTrue(session.State.AgentMemories.All(m => m.Plan is not null));
            Assert.AreEqual(original, JsonSerializer.Serialize(session.State.PendingEvent), "后台规划不得改写议事正文、选项、成本或效果。");
            Assert.AreEqual(revision, session.State.Revision);
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    [DataRow("rules")]
    [DataRow("llm")]
    public async Task PendingCouncilAloneDoesNotMakeAnyModelRequest(string narrativeSource)
    {
        var root = NewDirectory();
        var handler = new FlowHandler();
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            var world = WorldFactory.Create("qingyun", 527, "llm", "normal", compact: true);
            world.PendingEvent!.NarrativeSource = narrativeSource;
            if (narrativeSource == "llm")
            {
                world.PendingEvent.Text = "旧档已生成的议事场景。";
                world.PendingEvent.Choices[0].Flavor = "旧档保留的选项说明。";
            }
            var originalCouncil = JsonSerializer.Serialize(world.PendingEvent);
            foreach (var sect in world.Sects.Where(s => s.Id != world.PlayerId))
            { sect.Eliminated = true; sect.Capital = ""; }
            foreach (var region in world.Regions.Where(r => r.Owner != world.PlayerId)) region.Owner = "";
            foreach (var person in world.People.Where(p => p.SectId != world.PlayerId)) person.Alive = false;
            await session.Commit(world);
            await session.Commit(await session.Saves.LoadAsync("auto"));
            for (var i = 0; i < 3; i++) await PumpAsync(session);
            Assert.IsFalse(session.HasBackgroundWork);
            Assert.AreEqual(0, handler.Calls, "仅有待决议事不能产生模型调用。");
            Assert.AreEqual(0, session.State!.ModelRequests);
            Assert.AreEqual(0, session.State.ModelContextChecks);
            Assert.AreEqual(originalCouncil, JsonSerializer.Serialize(session.State.PendingEvent), "载入后保留既有文案，不为其重新调用模型。");
            var initialState = session.State!;
            var peopleBefore = initialState.People.Count;
            var trainingBefore = initialState.Player.Training;

            await session.Execute(initialState.Revision, new GameCommand("choose", Option: "expansion"), CancellationToken.None);
            Assert.IsNull(session.State!.PendingEvent);
            Assert.AreEqual("llm", session.State.Mode);
            Assert.AreEqual(string.Empty, session.State.AgentStatus);
            Assert.HasCount(peopleBefore + 3, session.State.People, "移除叙事调用不能改变规则选项效果。");
            Assert.AreEqual(trainingBefore + 1, session.State.Player.Training);
            Assert.AreEqual(0, handler.Calls);
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task TransientFailureRecoversAndCompletedSectsAreNotScheduledAgain()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(failSect: "chixia", blockFinalPlanSect: "baicao");
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            await session.NewGame("qingyun", 8192, "llm", "normal", CancellationToken.None);
            await handler.BlockedPlan.WaitAsync(TimeSpan.FromSeconds(5));

            await PumpUntilAsync(session, () =>
            {
                var state = session.State!;
                var expectedCompletedCount = state.Sects.Count(sect => sect.Id != state.PlayerId && sect.Id != "baicao");
                return state.AgentMemories.Count(memory => memory.SectId != "baicao") == expectedCompletedCount
                    && state.AgentMemories.Where(memory => memory.SectId != "baicao").All(memory => memory.Plan is not null);
            }, TimeSpan.FromSeconds(12));
            Assert.IsTrue(session.HasBackgroundWork, "百草谷的独立计划仍在等待时，其他已完成宗门应保留完成状态。");
            Assert.AreEqual(1, handler.FailuresFor("chixia"), "一次失败后该宗门应由后台重试恢复。");

            var completed = session.State!.AgentMemories.Where(m => m.SectId != "baicao").Select(m => m.SectId).ToArray();
            var requestCounts = completed.ToDictionary(id => id, handler.RequestsFor);
            for (var i = 0; i < 4; i++) await PumpAsync(session);
            foreach (var sect in completed)
                Assert.AreEqual(requestCounts[sect], handler.RequestsFor(sect), $"已完成宗门 {sect} 不应被重复唤醒。");

            handler.ReleaseBlockedResponses();
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(8));
            Assert.IsTrue(session.State.AgentMemories.All(m => m.Plan is not null));
            Assert.AreEqual("llm", session.State.Mode);
            Assert.AreEqual(string.Empty, session.State.AgentStatus);
            Assert.IsFalse(JsonSerializer.Serialize(session.State).Contains("TEST_ONLY", StringComparison.Ordinal));
            Assert.IsFalse(JsonSerializer.Serialize(session.State).Contains("TEST_PRIVATE_REASONING", StringComparison.Ordinal));
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task ExhaustedAgentRetriesSwitchWorldOfflineAndCancelOtherWork()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(failSect: "chixia", failAlways: true, blockFinalPlanSect: "baicao");
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            await session.NewGame("qingyun", 1441, "llm", "normal", CancellationToken.None);
            await handler.BlockedPlan.WaitAsync(TimeSpan.FromSeconds(5));

            await PumpUntilAsync(session, () => session.State!.Mode == "rules", TimeSpan.FromSeconds(20));
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(5));

            Assert.AreEqual(4, handler.FailuresFor("chixia"), "初次失败加最多三次重试，总计四轮。");
            Assert.AreEqual("rules", session.State!.Mode);
            Assert.IsTrue(session.State.AgentStatus.Contains("重试3次", StringComparison.Ordinal));
            Assert.IsTrue(session.State.AgentStatus.Contains("离线", StringComparison.Ordinal));
            Assert.IsTrue(session.State.History.Any(e => e.Kind == "agent"));
            Assert.IsGreaterThan(0, handler.CanceledBlockedRequests, "耗尽后应取消仍在等待的其他宗门任务。");

            var saved = await session.Saves.LoadAsync("auto");
            Assert.AreEqual("rules", saved.Mode);
            Assert.IsTrue(saved.AgentStatus.Contains("重试3次", StringComparison.Ordinal));
            Assert.IsTrue(saved.History.Any(e => e.Kind == "agent"));
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task LoadingSavedWorldDiscardsLateBackgroundResponse()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(blockFinalPlanSect: "chixia", ignoreCancellationForBlockedPlan: true);
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            await session.NewGame("qingyun", 2201, "llm", "normal", CancellationToken.None);
            await handler.BlockedPlan.WaitAsync(TimeSpan.FromSeconds(5));

            var loadedMarker = "载入存档后的世界叙事";
            var snapshot = session.State!.Copy();
            snapshot.Mode = "rules";
            snapshot.LastCouncil = loadedMarker;
            await session.Saves.SaveAsync(snapshot, "one");
            await session.Commit(await session.Saves.LoadAsync("one"));
            var loadedRevision = session.State!.Revision;

            handler.ReleaseBlockedResponses();
            await handler.LatePlanResponse.WaitAsync(TimeSpan.FromSeconds(5));
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(5));

            Assert.AreEqual(loadedMarker, session.State!.LastCouncil);
            Assert.AreEqual("rules", session.State.Mode);
            Assert.AreEqual(loadedRevision, session.State.Revision, "旧请求返回不能覆写载入世界或增加 Revision。");
            Assert.IsFalse(session.State.AgentMemories.Any(m => m.SectId == "chixia" && m.Plan is not null));
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task PlanTargetThatBecomesOwnedBeforePumpIsRejectedAndAgentReobserves()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(targetSect: "chixia", blockFinalPlanSect: "chixia");
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root);
            await session.NewGame("qingyun", 7771, "llm", "normal", CancellationToken.None);
            await handler.BlockedPlan.WaitAsync(TimeSpan.FromSeconds(5));
            var oldTarget = handler.PlanTargetsFor("chixia").LastOrDefault();
            Assert.IsFalse(string.IsNullOrEmpty(oldTarget), "测试模型应从该宗快照的合法目标中选择目标。");

            await session.Gate.WaitAsync();
            try
            {
                var target = session.State!.Regions.Single(r => r.Id == oldTarget);
                target.Owner = "chixia";
                GameEngine.Validate(session.State);
            }
            finally { session.Gate.Release(); }

            handler.ReleaseBlockedResponses();
            await PumpUntilAsync(session, () => handler.PlanTargetsFor("chixia").Count >= 2
                && !session.HasBackgroundWork, TimeSpan.FromSeconds(10));

            var targets = handler.PlanTargetsFor("chixia");
            Assert.AreNotEqual(oldTarget, targets[1], "再次唤醒时必须基于新快照选取目标。");
            Assert.AreEqual(targets[1], session.State!.AgentMemories.Single(m => m.SectId == "chixia").Plan!.Target);
            Assert.AreNotEqual(oldTarget, session.State.Sects.Single(s => s.Id == "chixia").Target,
                "旧的过期计划不能覆盖世界中的宗门目标。");
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task ConfiguredConcurrencyLimitIncludesSectAndContextRequests()
    {
        var root = NewDirectory();
        var handler = new FlowHandler(responseDelayMilliseconds: 20);
        using var client = new HttpClient(handler);
        var session = new GameSession(root, client);
        try
        {
            await ConfigureAsync(root, maxConcurrentRequests: 2);
            await session.NewGame("qingyun", 8821, "llm", "normal", CancellationToken.None);
            var revision = session.State!.Revision;
            await PumpUntilAsync(session, () => !session.HasBackgroundWork, TimeSpan.FromSeconds(15));

            Assert.AreEqual(0, handler.CouncilCalls, "议事不能占用后台模型并发。");
            Assert.IsTrue(handler.PeakConcurrency is > 1 and <= 2,
                "宗门Agent及上下文请求的并发均不得超过 runtime.maxConcurrentRequests。");
            Assert.AreEqual(revision, session.State!.Revision, "Pump 收集记忆不能增加 Revision。");
        }
        finally
        {
            handler.ReleaseBlockedResponses();
            session.StopAgents();
            await DrainAsync(session);
            Directory.Delete(root, true);
        }
    }

    private static string NewDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "infinite-dao-async-agents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task ConfigureAsync(string root, int maxConcurrentRequests = 10)
    {
        Directory.CreateDirectory(Path.Combine(root, "config"));
        var configuration = new LlmConfiguration
        {
            Endpoint = "https://provider.example/v1/messages", Model = "test", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "",
            Runtime = new LlmRuntimeConfiguration { MaxConcurrentRequests = maxConcurrentRequests }
        };
        await File.WriteAllTextAsync(Path.Combine(root, "config", "llm.local.json"),
            JsonSerializer.Serialize(configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static async Task PumpAsync(GameSession session)
    {
        await session.Gate.WaitAsync();
        try { await session.PumpAgents(CancellationToken.None); }
        finally { session.Gate.Release(); }
    }

    private static async Task PumpUntilAsync(GameSession session, Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await PumpAsync(session);
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.IsTrue(condition(), "后台Agent未在限定时间内达到预期状态。");
    }

    private static async Task DrainAsync(GameSession session)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (session.HasBackgroundWork && DateTime.UtcNow < deadline) await Task.Delay(10);
    }

    private sealed class FlowHandler(
        string? failSect = null,
        bool failAlways = false,
        string? blockFinalPlanSect = null,
        bool ignoreCancellationForBlockedPlan = false,
        bool blockAllResponses = false,
        string? targetSect = null,
        int responseDelayMilliseconds = 0) : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> firstModelRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> blockedPlan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> latePlanResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<string, int> requestsBySect = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, int> failuresBySect = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> planTargets = new(StringComparer.Ordinal);
        private int calls, active, peak, councilCalls, blockedOnce, canceledBlockedRequests;

        public Task FirstModelRequest => firstModelRequest.Task;
        public Task BlockedPlan => blockedPlan.Task;
        public Task LatePlanResponse => latePlanResponse.Task;
        public int Calls => Volatile.Read(ref calls);
        public int PeakConcurrency => Volatile.Read(ref peak);
        public int CouncilCalls => Volatile.Read(ref councilCalls);
        public int CanceledBlockedRequests => Volatile.Read(ref canceledBlockedRequests);
        public int RequestsFor(string sect) => requestsBySect.GetValueOrDefault(sect);
        public int FailuresFor(string sect) => failuresBySect.GetValueOrDefault(sect);
        public IReadOnlyList<string> PlanTargetsFor(string sect) => planTargets.TryGetValue(sect, out var queue) ? queue.ToArray() : [];

        public void ReleaseBlockedResponses() => release.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var concurrent = Interlocked.Increment(ref active);
            UpdatePeak(concurrent);
            try
            {
                var path = request.RequestUri!.AbsolutePath;
                using var envelope = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                if (path.EndsWith("/count_tokens", StringComparison.Ordinal))
                {
                    if (responseDelayMilliseconds > 0) await Task.Delay(responseDelayMilliseconds, cancellationToken);
                    return JsonResponse(new { input_tokens = 500 });
                }

                Interlocked.Increment(ref calls);
                firstModelRequest.TrySetResult(true);
                var root = envelope.RootElement;
                var messages = root.GetProperty("messages");
                var actor = FindActor(messages);
                var lastContent = LastUserContent(messages);
                var isCouncil = root.TryGetProperty("tools", out var tools)
                    && tools.EnumerateArray().Any(t => t.GetProperty("name").GetString() == "submit_council");
                var isPlan = !isCouncil && lastContent.ValueKind == JsonValueKind.Array;
                var target = isPlan && actor == targetSect ? FirstValidTarget(messages, actor!) : "";

                if (actor is not null) requestsBySect.AddOrUpdate(actor, 1, (_, count) => count + 1);
                if (isPlan && actor is not null)
                    planTargets.GetOrAdd(actor, _ => new ConcurrentQueue<string>()).Enqueue(target);
                if (isCouncil)
                {
                    Interlocked.Increment(ref councilCalls);
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Council narration must not be requested.") };
                }
                if (actor is not null && actor == failSect && (failAlways || !failuresBySect.ContainsKey(actor)))
                {
                    failuresBySect.AddOrUpdate(actor!, 1, (_, count) => count + 1);
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("synthetic unavailable") };
                }

                var shouldBlockPlan = isPlan && actor == blockFinalPlanSect && Interlocked.CompareExchange(ref blockedOnce, 1, 0) == 0;
                if (blockAllResponses || shouldBlockPlan)
                {
                    if (shouldBlockPlan) blockedPlan.TrySetResult(true);
                    try
                    {
                        if (shouldBlockPlan && ignoreCancellationForBlockedPlan) await release.Task;
                        else await release.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref canceledBlockedRequests);
                        throw;
                    }
                }

                if (responseDelayMilliseconds > 0) await Task.Delay(responseDelayMilliseconds, cancellationToken);
                if (!isPlan)
                    return JsonResponse(new
                    {
                        type = "message", role = "assistant", stop_reason = "tool_use",
                        content = new object[]
                        {
                            new { type = "thinking", thinking = "TEST_PRIVATE_REASONING", signature = "opaque-test" },
                            new { type = "tool_use", id = "inspect", name = "get_situation", input = new { section = "history" } }
                        },
                        usage = new { input_tokens = 1, output_tokens = 2 }
                    });

                var response = JsonResponse(new
                {
                    type = "message", role = "assistant", stop_reason = "tool_use",
                    content = new object[]
                    {
                        new
                        {
                            type = "tool_use", id = "submit", name = "submit_strategy",
                            input = new
                            {
                                policy = "balanced", target, goal = "稳固道统", reason = "休养门人，整备长远发展。",
                                stages = new[] { "保障供养", "培养核心修士" }, reconsiderWhen = "遭遇战争或核心战力变化", reviewAfterMonths = 60
                            }
                        }
                    },
                    usage = new { input_tokens = 1, output_tokens = 2 }
                });
                if (shouldBlockPlan) latePlanResponse.TrySetResult(true);
                return response;
            }
            finally { Interlocked.Decrement(ref active); }
        }

        private void UpdatePeak(int concurrent)
        {
            int current;
            do
            {
                current = Volatile.Read(ref peak);
                if (concurrent <= current) return;
            } while (Interlocked.CompareExchange(ref peak, concurrent, current) != current);
        }

        private static string? FindActor(JsonElement messages)
        {
            foreach (var message in messages.EnumerateArray().Reverse())
            {
                if (message.GetProperty("role").GetString() != "user" || message.GetProperty("content").ValueKind != JsonValueKind.String) continue;
                try
                {
                    using var data = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                    if (data.RootElement.TryGetProperty("current", out var current)
                        && current.TryGetProperty("agents", out var agents) && agents.GetArrayLength() > 0)
                        return agents[0].GetProperty("sectId").GetString();
                }
                catch (JsonException) { }
            }
            return null;
        }

        private static JsonElement LastUserContent(JsonElement messages)
        {
            var last = default(JsonElement);
            foreach (var message in messages.EnumerateArray())
                if (message.GetProperty("role").GetString() == "user") last = message.GetProperty("content");
            return last;
        }

        private static string FirstValidTarget(JsonElement messages, string actor)
        {
            foreach (var message in messages.EnumerateArray().Reverse())
            {
                if (message.GetProperty("role").GetString() != "user" || message.GetProperty("content").ValueKind != JsonValueKind.String) continue;
                try
                {
                    using var data = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                    if (!data.RootElement.TryGetProperty("current", out var current) || !current.TryGetProperty("agents", out var agents)) continue;
                    var agent = agents.EnumerateArray().Single(a => a.GetProperty("sectId").GetString() == actor);
                    return agent.GetProperty("validTargets").EnumerateArray().Select(t => t.GetProperty("id").GetString()).FirstOrDefault(id => id is not null) ?? "";
                }
                catch (JsonException) { }
            }
            return "";
        }

        private static HttpResponseMessage JsonResponse<T>(T value) =>
            new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };

    }
}
