using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfiniteDao.Agents.Configuration;

namespace InfiniteDao.Agents.Providers;

internal sealed class AnthropicJsonClient(HttpClient client, Func<CancellationToken, Task>? onContextCheck = null)
{
    internal async Task<(JsonElement Data, long Tokens)> Send(LlmConfiguration config, object context, string system, CancellationToken cancellationToken)
    {
        var reply = await SendMessage(config, AnthropicRequestBuilder.Build(config, JsonSerializer.Serialize(context), system), cancellationToken);
        if (reply.Message.GetProperty("stop_reason").GetString() == "tool_use") throw new ModelDecisionException("此流程未授权工具调用，世界保持暂停。");
        try
        {
            var text = string.Concat(reply.Message.GetProperty("content").EnumerateArray().Where(b => b.GetProperty("type").GetString() == "text").Select(b => b.GetProperty("text").GetString())).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var start = text.IndexOf('\n'); var end = text.LastIndexOf("```", StringComparison.Ordinal);
                if (start < 0 || end <= start) throw new JsonException(); text = text[(start + 1)..end].Trim();
            }
            using var parsed = JsonDocument.Parse(text); return (parsed.RootElement.Clone(), reply.Tokens);
        }
        catch (JsonException error)
        { throw new ModelDecisionException($"模型文本不是合法 JSON（行 {error.LineNumber}，字节位置 {error.BytePositionInLine}），世界保持暂停；请明确重试。未记录响应原文或隐藏思考。"); }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException)
        { throw new ModelDecisionException("模型响应格式不完整，世界保持暂停。不会采纳未验证内容，请明确重试或选择离线。"); }
    }

    internal static JsonObject ToolRequest(LlmConfiguration config, object context, string system, string name, string description, JsonObject schema)
    {
        var payload = AnthropicRequestBuilder.Build(config, JsonSerializer.Serialize(context), system);
        payload["tools"] = new JsonArray(new JsonObject { ["name"] = name, ["description"] = description, ["strict"] = true, ["input_schema"] = schema.DeepClone() });
        payload["tool_choice"] = new JsonObject { ["type"] = "auto" };
        return payload;
    }

    internal async Task<(JsonElement Data, long Tokens)> SendTool(LlmConfiguration config, object context, string system, string name, string description, JsonObject schema, CancellationToken cancellationToken)
    {
        var reply = await SendMessage(config, ToolRequest(config, context, system, name, description, schema), cancellationToken);
        return (Submission(reply.Message, name), reply.Tokens);
    }

    internal static JsonElement Submission(JsonElement message, string name)
    {
        var tools = message.GetProperty("content").EnumerateArray().Where(b => b.GetProperty("type").GetString() == "tool_use").ToArray();
        if (message.GetProperty("stop_reason").GetString() != "tool_use" || tools.Length != 1 || tools[0].GetProperty("name").GetString() != name
            || !tools[0].TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
            throw new ModelDecisionException($"模型未通过 {name} 工具提交唯一结构化结果，世界保持暂停；不会尝试从普通文本提取 JSON。");
        return input.Clone();
    }

    internal async Task<(JsonElement Message, long Tokens)> SendMessage(LlmConfiguration config, JsonObject payload, CancellationToken cancellationToken)
    {
        var message = await Exchange(config, new Uri(config.Endpoint), payload, cancellationToken);
        try
        {
            if (message.GetProperty("type").GetString() != "message" || message.GetProperty("role").GetString() != "assistant") throw new JsonException();
            if (message.GetProperty("stop_reason").GetString() is not ("end_turn" or "tool_use"))
                throw new ModelDecisionException("模型响应截断、拒绝或未完成，世界保持暂停；请检查输出预算后明确重试。");
            var content = message.GetProperty("content");
            if (content.ValueKind != JsonValueKind.Array) throw new JsonException();
            foreach (var block in content.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "tool_use" && (string.IsNullOrWhiteSpace(block.GetProperty("id").GetString())
                    || string.IsNullOrWhiteSpace(block.GetProperty("name").GetString()) || block.GetProperty("input").ValueKind != JsonValueKind.Object)) throw new JsonException();
                if (type == "text" && block.GetProperty("text").ValueKind != JsonValueKind.String) throw new JsonException();
            }
            long tokens = 0;
            if (message.TryGetProperty("usage", out var usage))
                foreach (var name in new[] { "input_tokens", "output_tokens", "cache_creation_input_tokens", "cache_read_input_tokens" })
                    if (usage.TryGetProperty(name, out var value) && value.TryGetInt64(out var n)) tokens += n;
            return (message, tokens);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new ModelDecisionException("模型返回了无效消息，世界保持暂停。"); }
    }

    internal async Task<long> CountTokens(LlmConfiguration config, JsonObject payload, CancellationToken cancellationToken)
    {
        if (onContextCheck is not null) await onContextCheck(cancellationToken);
        var endpoint = new UriBuilder(config.Endpoint); endpoint.Path = endpoint.Path.TrimEnd('/') + "/count_tokens";
        var body = payload.DeepClone().AsObject(); body.Remove("max_tokens"); body.Remove("stream"); body.Remove("temperature");
        var reply = await Exchange(config, endpoint.Uri, body, cancellationToken);
        if (!reply.TryGetProperty("input_tokens", out var value) || !value.TryGetInt64(out var tokens) || tokens < 0)
            throw new ModelDecisionException("上下文计数接口未返回合法 input_tokens，世界保持暂停。需要支持 Anthropic messages/count_tokens。");
        return tokens;
    }

    private async Task<JsonElement> Exchange(LlmConfiguration config, Uri endpoint, JsonObject payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("anthropic-version", config.AnthropicVersion);
        var key = config.ResolveApiKey(Environment.GetEnvironmentVariable);
        if (config.AuthMode == "x-api-key") request.Headers.Add("x-api-key", key);
        else if (config.AuthMode == "bearer") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.Request!.TimeoutSeconds));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new ModelDecisionException($"{(endpoint.AbsolutePath.EndsWith("/count_tokens", StringComparison.Ordinal) ? "上下文计数接口" : "模型接口")}返回 HTTP {(int)response.StatusCode}，世界已暂停。请检查配置后明确重试；不会自动降级。");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (memory.Length + read > 2_000_000) throw new ModelDecisionException("模型响应过大，已拒绝，世界保持暂停。");
                memory.Write(buffer, 0, read);
            }
            using var envelope = JsonDocument.Parse(memory.ToArray()); return envelope.RootElement.Clone();
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
        { throw new ModelDecisionException("模型连接或读取失败、超时，世界保持暂停。请求可能已计费；不会自动重试或切换离线。"); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new ModelDecisionException("模型响应格式不完整，世界保持暂停。不会采纳未验证内容，请明确重试或选择离线。"); }
    }
}
