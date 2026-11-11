using System.Net;
using System.Text.Json;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Core.Game;
using InfiniteDao.Application;
using InfiniteDao.Infrastructure.Persistence;

namespace InfiniteDao.IntegrationTests;

[TestClass]
public sealed class GamePersistenceTests
{
    [TestMethod]
    public async Task SaveLoadAndContinuedWorldMatchUninterruptedRun()
    {
        var root = NewDirectory();
        try
        {
            var store = new GameSaveStore(root);
            var world = WorldFactory.Create("baicao", 441, "rules", "normal");
            GameEngine.Command(world, new GameCommand("choose", Option: "cultivation"));
            for (var i = 0; i < 15; i++) Tick(world);
            await store.SaveAsync(world, "one");
            var restored = await store.LoadAsync("one");
            for (var i = 0; i < 20; i++) { Tick(world); Tick(restored); }
            Assert.AreEqual(JsonSerializer.Serialize(world), JsonSerializer.Serialize(restored));
            Assert.HasCount(1, await store.ListAsync());
            await Assert.ThrowsAsync<GameRuleException>(() => store.LoadAsync("../anything"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ImportedMarkupOrForgedSectDefinitionsAreRejected()
    {
        var store = new GameSaveStore("unused");
        var world = WorldFactory.Create("qingyun", 441, "rules", "normal");
        world.Regions[0].Name = "<img src=x onerror=alert(1)>";
        Assert.Throws<GameRuleException>(() => store.Parse(JsonSerializer.Serialize(world)));
        world = WorldFactory.Create("qingyun", 441, "rules", "normal");
        world.Sects[0].Color = "red; background:url(https://example.test)";
        Assert.Throws<GameRuleException>(() => store.Parse(JsonSerializer.Serialize(world)));
        Assert.Throws<GameRuleException>(() => store.Parse("{\"sects\":null}"));
    }

    [TestMethod]
    public async Task FailedBackgroundProviderDoesNotAbortOrReplaceCommittedMonth()
    {
        var root = NewDirectory();
        GameSession? session = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "config"));
            var config = new LlmConfiguration { Endpoint = "https://provider.example/v1/messages", Model = "test", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "", Runtime = new() { MaxRequestsPerRun = 1, MaxConcurrentRequests = 1 } };
            await File.WriteAllTextAsync(Path.Combine(root, "config", "llm.local.json"), JsonSerializer.Serialize(config));
            var handler = new RejectHandler(); using var client = new HttpClient(handler);
            session = new GameSession(root, client);
            var world = WorldFactory.Create("qingyun", 42, "llm", "normal");
            GameEngine.Command(world, new GameCommand("choose", Option: "expansion"));
            await session.Commit(world);
            var priorRevision = world.Revision;
            await session.Advance(priorRevision, CancellationToken.None);
            await handler.FirstFailure.WaitAsync(TimeSpan.FromSeconds(5));
            await session.Gate.WaitAsync();
            try { await session.PumpAgents(CancellationToken.None); }
            finally { session.Gate.Release(); }

            Assert.AreEqual(1, session.State!.Month, "后台模型失败不得阻止已提交的月结。");
            Assert.AreEqual(priorRevision + 1, session.State.Revision);
            Assert.AreEqual("llm", session.State.Mode, "单次失败只进入重试，不应立即降级或回滚世界。");
            Assert.IsGreaterThan(0, session.State.ModelRequests);
            Assert.IsGreaterThan(0, handler.Calls);
            var saved = await session.Saves.LoadAsync("auto");
            Assert.AreEqual(1, saved.Month);
            Assert.AreEqual(session.State.Revision, saved.Revision);
        }
        finally
        {
            session?.StopAgents();
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task ExplicitSaveDirectoryKeepsVerificationSeparateFromPlayerSaves()
    {
        var root = NewDirectory();
        try
        {
            using var client = new HttpClient(new RejectHandler());
            var saves = Path.Combine(root, "isolated-saves");
            var session = new GameSession(root, client, saves);
            await session.Commit(WorldFactory.Create("qingyun", 42, "rules", "normal"));
            Assert.IsTrue(File.Exists(Path.Combine(saves, "auto.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "artifacts", "game", "saves")));
            Assert.AreEqual(42, (await session.Saves.LoadAsync("auto")).Seed);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task NativeModelStatusHasTypedEffortAndNoCredentials()
    {
        var root = NewDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "config"));
            var config = new LlmConfiguration { Endpoint = "https://provider.example/v1/messages", Model = "test", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "", Request = new() { ThinkingMode = "adaptive", ReasoningEffort = "high" } };
            await File.WriteAllTextAsync(Path.Combine(root, "config", "llm.local.json"), JsonSerializer.Serialize(config));
            using var client = new HttpClient(new RejectHandler());
            var status = new GameSession(root, client).ModelStatus();
            Assert.IsTrue(status.Configured); Assert.AreEqual("high", status.Effort);
            Assert.IsFalse(JsonSerializer.Serialize(status).Contains("TEST_ONLY", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "infinite-dao-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); return root;
    }

    [TestMethod]
    [DataRow("hidden")]
    [DataRow("wander")]
    [DataRow("revenge")]
    public void EveryEmberBranchCanRebuildWithoutAnyUnownedLand(string choice)
    {
        var world = WorldFactory.Create("qingyun", 23, "rules", "normal");
        world.PendingEvent = null;
        foreach (var region in world.Regions.Where(r => r.Owner == "" || r.Owner == world.PlayerId)) region.Owner = "chixia";
        var lastGate = world.Regions.Single(r => r.Id == "r07");
        lastGate.Owner = world.PlayerId; world.Player.Capital = lastGate.Id;
        world.Player.Stones = 0; world.Player.Herbs = 0;
        world.PendingEvent = new GameEvent { Id = world.NewId(), Type = "defense", Actor = "chixia", Target = lastGate.Id, Choices = [new EventChoice { Id = "retreat" }] };
        GameEngine.Command(world, new GameCommand("choose", Option: "retreat"));
        Assert.IsTrue(world.Player.Exiled); Assert.IsGreaterThanOrEqualTo(350, world.Player.Stones);
        GameEngine.Command(world, new GameCommand("choose", Option: choice));
        var purchased = world.Regions.First(r => r.Id != world.Sects.Single(s => s.Id == r.Owner).Capital);
        GameEngine.Command(world, new GameCommand("settle", purchased.Id));
        Assert.IsFalse(world.Player.Exiled); Assert.AreEqual(world.PlayerId, purchased.Owner);
        Assert.IsNotEmpty(GameEngine.Members(world, world.Player)); GameEngine.Validate(world);
    }

    [TestMethod]
    public void WaterHeritageAndDiplomaticPolicyHaveRealMechanicalEffects()
    {
        var world = WorldFactory.Create("jingshui", 49, "rules", "normal");
        Assert.AreEqual(2, world.Player.Intel);
        Assert.IsTrue(world.Player.Relations.Values.All(value => value == 10));
        GameEngine.Command(world, new GameCommand("choose", Option: "prosperity"));
        GameEngine.Command(world, new GameCommand("policy", Option: "diplomacy"));
        var before = world.Player.Relations["qingyun"];
        GameEngine.AdvanceMonth(world);
        Assert.IsGreaterThan(before, world.Player.Relations["qingyun"]);
    }
    private static void Tick(GameState state)
    {
        if (state.Status != "playing") return;
        if (state.PendingEvent is { } e)
        {
            var choice = e.Choices.Last(c => c.Stones <= state.Player.Stones && c.Herbs <= state.Player.Herbs);
            GameEngine.Command(state, new GameCommand("choose", Option: choice.Id));
        }
        if (state.Player.Exiled)
        {
            var region = state.Regions.FirstOrDefault(r => r.Owner == "") ?? state.Regions.First(r => state.Sects.Single(s => s.Id == r.Owner).Capital != r.Id);
            GameEngine.Command(state, new GameCommand("settle", region.Id));
        }
        if (state.PendingEvent is null && state.Status == "playing") GameEngine.AdvanceMonth(state);
    }
    private sealed class RejectHandler : HttpMessageHandler
    {
        private int calls;
        private readonly TaskCompletionSource<bool> firstFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls => Volatile.Read(ref calls);
        public Task FirstFailure => firstFailure.Task;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/count_tokens", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":500}") });
            Interlocked.Increment(ref calls);
            firstFailure.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("synthetic unavailable") });
        }
    }
}
