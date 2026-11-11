using System.Net;
using System.Text.Json;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Agents.Providers;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Agents.Tests;

[TestClass]
public sealed class CouncilToolTests
{
    [TestMethod]
    public async Task NarrationUsesStrictToolAndCannotChangeRuleEffects()
    {
        var world = WorldFactory.Create("qingyun", 87, "rules", "normal"); var council = world.PendingEvent!;
        var effects = council.Choices.Select(c => (c.Id, c.Description, c.Stones, c.Herbs)).ToArray(); var stones = world.Player.Stones;
        var input = new { title = "开宗议事", text = "群山钟鸣，掌门请诸位长老议定方向。", choices = council.Choices.Select(c => new { id = c.Id, flavor = "谨慎衡量利弊。" }).ToArray() };
        var handler = new ReplyHandler(input); using var client = new HttpClient(handler);
        await new CouncilNarratorProvider(client).NarrateAsync(world, Config(), CancellationToken.None);
        Assert.AreEqual("llm", council.NarrativeSource); Assert.AreEqual(stones, world.Player.Stones);
        CollectionAssert.AreEqual(effects, council.Choices.Select(c => (c.Id, c.Description, c.Stones, c.Herbs)).ToArray());
        var tool = handler.Request.GetProperty("tools")[0];
        Assert.AreEqual("submit_council", tool.GetProperty("name").GetString()); Assert.IsTrue(tool.GetProperty("strict").GetBoolean());
        Assert.AreEqual("auto", handler.Request.GetProperty("tool_choice").GetProperty("type").GetString(), "保留HIGH思考，不使用不兼容的强制tool_choice。");
        CollectionAssert.AreEqual(council.Choices.Select(c => c.Id).ToArray(), tool.GetProperty("input_schema").GetProperty("properties").GetProperty("choices").GetProperty("items").GetProperty("properties").GetProperty("id").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [TestMethod]
    public async Task CorrectJsonInPlainTextIsRejectedInsteadOfBeingParsed()
    {
        var world = WorldFactory.Create("qingyun", 87, "rules", "normal"); var title = world.PendingEvent!.Title;
        using var client = new HttpClient(new ReplyHandler(new { title = "不能采用", text = "不能从文本解析", choices = Array.Empty<object>() }) { PlainText = true });
        var error = await Assert.ThrowsAsync<ModelDecisionException>(() => new CouncilNarratorProvider(client).NarrateAsync(world, Config(), CancellationToken.None));
        StringAssert.Contains(error.Message, "submit_council"); Assert.AreEqual(title, world.PendingEvent.Title);
        Assert.AreNotEqual("llm", world.PendingEvent.NarrativeSource);
    }

    [TestMethod]
    public async Task ToolFormatDoesNotBypassChoiceIdentityValidation()
    {
        var world = WorldFactory.Create("qingyun", 87, "rules", "normal"); var title = world.PendingEvent!.Title;
        using var client = new HttpClient(new ReplyHandler(new { title = "不能采用", text = "内容合法但选项被篡改", choices = new[] { new { id = "invented", flavor = "伪造选项" } } }));
        await Assert.ThrowsAsync<ModelDecisionException>(() => new CouncilNarratorProvider(client).NarrateAsync(world, Config(), CancellationToken.None));
        Assert.AreEqual(title, world.PendingEvent.Title); Assert.AreNotEqual("llm", world.PendingEvent.NarrativeSource);
    }

    private static LlmConfiguration Config() => new() { Endpoint = "https://provider.example/v1/messages", Model = "test", ApiKey = "TEST_ONLY", ApiKeyEnvironmentVariable = "" };
    private sealed class ReplyHandler(object input) : HttpMessageHandler
    {
        public bool PlainText { get; init; }
        public JsonElement Request { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)); Request = document.RootElement.Clone();
            object[] content = PlainText ? [new { type = "text", text = JsonSerializer.Serialize(input) }] : [new { type = "tool_use", id = "council", name = "submit_council", input }];
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { type = "message", role = "assistant", stop_reason = PlainText ? "end_turn" : "tool_use", content, usage = new { input_tokens = 100, output_tokens = 20 } })) };
        }
    }
}
