using System.Text.Json.Nodes;
using InfiniteDao.Agents.Configuration;

namespace InfiniteDao.Agents.Providers;

public static class AnthropicRequestBuilder
{
    // Configuration validation belongs at the host boundary, before building requests.
    public static JsonObject Build(LlmConfiguration configuration, string userMessage, string? system = null)
    {
        var options = configuration.Request
            ?? throw new ArgumentException("request configuration is required.", nameof(configuration));
        var body = new JsonObject
        {
            ["model"] = configuration.Model,
            ["max_tokens"] = options.MaxOutputTokens,
            ["stream"] = false,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = userMessage })
        };
        if (system is not null)
            body["system"] = system;
        if (options.Temperature is not null)
            body["temperature"] = options.Temperature;
        if (options.ThinkingMode is not null)
            body["thinking"] = new JsonObject { ["type"] = options.ThinkingMode };
        if (options.ReasoningEffort is not null)
            body["output_config"] = new JsonObject { ["effort"] = options.ReasoningEffort };
        return body;
    }
}
