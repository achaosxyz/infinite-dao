using System.Net;
using System.Text.Json;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Agents.Providers;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Agents.Tests;

[TestClass]
public sealed class SectStrategyProviderTests
{
    private static LlmConfiguration Settings() => new() { Endpoint = "https://provider.example/v1/messages", Model = "test-model", ApiKey = "TEST_ONLY_CREDENTIAL", ApiKeyEnvironmentVariable = "" };

    [TestMethod]
    public async Task AnthropicNativeRequestCarriesHighEffortAndReturnsOnePlanPerLivingRival()
    {
        var state = WorldFactory.Create("qingyun", 17, "llm", "normal");
        var decisions = state.Sects.Where(s => s.Id != state.PlayerId).Select(s => new { sectId = s.Id, policy = "prosperity", target = "", reason = "积蓄实力，等待时机。" });
        var response = JsonSerializer.Serialize(new { type = "message", role = "assistant", stop_reason = "end_turn", content = new object[] { new { type = "thinking", thinking = "not included in returned proposals" }, new { type = "text", text = JsonSerializer.Serialize(new { decisions }) } }, usage = new { input_tokens = 90, output_tokens = 180 } });
        var handler = new FakeHandler(response); using var client = new HttpClient(handler);
        var result = await new SectStrategyProvider(client).DecideAsync(state,Settings(),CancellationToken.None);
        Assert.HasCount(state.Sects.Count - 1,result.Decisions); Assert.AreEqual(270L,result.Tokens); Assert.AreEqual(1,handler.Calls);
        using var request = JsonDocument.Parse(handler.Request);
        Assert.AreEqual("high",request.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.AreEqual("adaptive",request.RootElement.GetProperty("thinking").GetProperty("type").GetString());
        Assert.IsFalse(handler.Request.Contains("TEST_ONLY_CREDENTIAL",StringComparison.Ordinal));
        Assert.AreEqual("TEST_ONLY_CREDENTIAL",handler.ApiKey);
    }

    [TestMethod]
    public async Task MissingPlansAreRejectedInsteadOfSilentRuleFallback()
    {
        var response = JsonSerializer.Serialize(new { stop_reason = "end_turn",content = new[] { new { type="text",text="{\"plans\":[]}" } } });
        var handler = new FakeHandler(response); using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<ModelDecisionException>(async()=>await new SectStrategyProvider(client).DecideAsync(WorldFactory.Create("qingyun",1,"llm","normal"),Settings(),CancellationToken.None));
        Assert.AreEqual(1,handler.Calls);
    }

    [TestMethod]
    public async Task HttpFailuresNeverExposeBodyOrRetry()
    {
        var handler = new FakeHandler("TEST_ONLY_CREDENTIAL provider-internal-details",HttpStatusCode.Unauthorized);using var client=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<ModelDecisionException>(async()=>await new SectStrategyProvider(client).DecideAsync(WorldFactory.Create("qingyun",1,"llm","normal"),Settings(),CancellationToken.None));
        Assert.DoesNotContain("TEST_ONLY_CREDENTIAL",error.Message);Assert.DoesNotContain("provider-internal-details",error.Message);Assert.AreEqual(1,handler.Calls);
    }

    [TestMethod]
    public async Task TokenTruncationCannotCommitPartialStrategicDecisions()
    {
        var handler=new FakeHandler("{\"type\":\"message\",\"role\":\"assistant\",\"stop_reason\":\"max_tokens\",\"content\":[]}");using var client=new HttpClient(handler);
        await Assert.ThrowsAsync<ModelDecisionException>(async()=>await new SectStrategyProvider(client).DecideAsync(WorldFactory.Create("qingyun",1,"llm","normal"),Settings(),CancellationToken.None));
    }

    private sealed class FakeHandler(string response,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        public int Calls {get;private set;}
        public string Request {get;private set;}="";
        public string ApiKey {get;private set;}="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;Request=await request.Content!.ReadAsStringAsync(cancellationToken);ApiKey=request.Headers.GetValues("x-api-key").Single();
            return new HttpResponseMessage(status){Content=new StringContent(response)};
        }
    }
}
