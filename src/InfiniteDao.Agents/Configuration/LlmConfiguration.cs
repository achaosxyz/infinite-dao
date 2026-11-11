using System.Globalization;

namespace InfiniteDao.Agents.Configuration;

// A class, not a record: generated record diagnostics must never print ApiKey.
public sealed class LlmConfiguration
{
    public string Protocol { get; init; } = "anthropic-messages";
    public string Endpoint { get; init; } = "";
    public string Model { get; init; } = "";
    public string AnthropicVersion { get; init; } = "2023-06-01";
    public string AuthMode { get; init; } = "x-api-key";
    public string ApiKey { get; init; } = "";
    public string ApiKeyEnvironmentVariable { get; init; } = "INFINITE_DAO_LLM_API_KEY";
    public LlmRequestConfiguration? Request { get; init; } = new();
    public LlmRuntimeConfiguration? Runtime { get; init; } = new();

    public string ResolveApiKey(Func<string, string?> readEnvironment)
    {
        var environmentKey = string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable)
            ? null
            : readEnvironment(ApiKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(environmentKey) ? ApiKey ?? "" : environmentKey;
    }

    public IReadOnlyList<string> Validate(Func<string, string?> readEnvironment)
    {
        List<string> errors = [];
        if (Protocol != "anthropic-messages")
            errors.Add("protocol must be anthropic-messages.");

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps
                && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback))
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment))
            errors.Add("endpoint must be an absolute HTTPS Messages URL without credentials, query or fragment; HTTP is allowed only on loopback.");

        if (string.IsNullOrWhiteSpace(Model) || Model.Any(char.IsControl))
            errors.Add("model must be a nonempty model identifier without control characters.");

        if (!DateOnly.TryParseExact(AnthropicVersion, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            errors.Add("anthropicVersion must be a valid yyyy-MM-dd API version date.");

        if (AuthMode is not ("x-api-key" or "bearer" or "none"))
            errors.Add("authMode must be x-api-key, bearer or none.");

        var validEnvironmentName = string.IsNullOrEmpty(ApiKeyEnvironmentVariable)
            || (ApiKeyEnvironmentVariable.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                && !char.IsAsciiDigit(ApiKeyEnvironmentVariable[0]));
        if (!validEnvironmentName)
            errors.Add("apiKeyEnvironmentVariable must be empty or an environment variable identifier.");

        var key = validEnvironmentName ? ResolveApiKey(readEnvironment) : ApiKey ?? "";
        if (AuthMode == "none")
        {
            if (!string.IsNullOrWhiteSpace(key))
                errors.Add("apiKey must be absent when authMode is none, including the configured environment variable.");
            if (endpoint is null || !endpoint.IsLoopback)
                errors.Add("authMode none is supported only for a loopback endpoint.");
        }
        else if (string.IsNullOrWhiteSpace(key))
            errors.Add("apiKey is required in the local file or the configured environment variable.");

        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
            errors.Add("apiKey must not contain whitespace or control characters.");

        if (Request is null)
            errors.Add("request must be an object.");
        else
        {
            if (Request.TimeoutSeconds is < 1 or > 600)
                errors.Add("request.timeoutSeconds must be between 1 and 600.");
            if (Request.MaxOutputTokens < 1)
                errors.Add("request.maxOutputTokens must be positive.");
            if (Request.Temperature is { } temperature
                && (!double.IsFinite(temperature) || temperature is < 0 or > 1))
                errors.Add("request.temperature must be null or between 0 and 1.");
            if (Request.ThinkingMode is not (null or "adaptive" or "enabled" or "disabled"))
                errors.Add("request.thinkingMode must be null, adaptive, enabled or disabled.");
            if (Request.ReasoningEffort is not (null or "low" or "medium" or "high" or "xhigh" or "max"))
                errors.Add("request.reasoningEffort must be null, low, medium, high, xhigh or max.");
            if (Request.ThinkingMode == "disabled" && Request.ReasoningEffort is not null)
                errors.Add("request.reasoningEffort must be null when thinking is disabled.");
        }

        if (Runtime is null)
            errors.Add("runtime must be an object.");
        else
        {
            if (Runtime.MaxConcurrentRequests < 1)
                errors.Add("runtime.maxConcurrentRequests must be positive.");
            if (Runtime.StrategyReviewMonths is < 12 or > 1200)
                errors.Add("runtime.strategyReviewMonths must be between 12 and 1200.");
            if (Runtime.ContextWindowTokens is < 8192 or > 2_000_000)
                errors.Add("runtime.contextWindowTokens must be between 8192 and 2000000.");
            if (!double.IsFinite(Runtime.CompactionThreshold) || Runtime.CompactionThreshold is < .5 or > .99)
                errors.Add("runtime.compactionThreshold must be between 0.5 and 0.99.");
            if (Runtime.MaxAgentSteps is < 1 or > 64)
                errors.Add("runtime.maxAgentSteps must be between 1 and 64 per wake.");
            if (Request is { MaxOutputTokens: > 0 } && Request.MaxOutputTokens >= Runtime.ContextWindowTokens * Runtime.CompactionThreshold)
                errors.Add("request.maxOutputTokens must fit below the context compaction threshold.");
            if (Runtime.FailurePolicy is not ("retry_then_rules" or "stop"))
                errors.Add("runtime.failurePolicy must be retry_then_rules (legacy stop is accepted).");
        }

        return errors;
    }
}

public sealed class LlmRequestConfiguration
{
    public int TimeoutSeconds { get; init; } = 90;
    public int MaxOutputTokens { get; init; } = 4096;
    public double? Temperature { get; init; }
    public string? ThinkingMode { get; init; } = "adaptive";
    public string? ReasoningEffort { get; init; } = "high";
}

public sealed class LlmRuntimeConfiguration
{
    // Limits simultaneously running sect Agent loops, not the total number of Agents.
    public int MaxConcurrentRequests { get; init; } = 10;
    public int StrategyReviewMonths { get; init; } = 60;
    public int ContextWindowTokens { get; init; } = 256000;
    public double CompactionThreshold { get; init; } = .9;
    public int MaxAgentSteps { get; init; } = 8;
    // Accepted only so existing private configuration keeps loading. Not a quota.
    public int MaxRequestsPerRun { get; init; }
    public string FailurePolicy { get; init; } = "retry_then_rules";
}
