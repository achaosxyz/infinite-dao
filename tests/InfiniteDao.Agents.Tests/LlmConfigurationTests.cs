using InfiniteDao.Agents.Configuration;

namespace InfiniteDao.Agents.Tests;

[TestClass]
public sealed class LlmConfigurationTests
{
    private const string TestKey = "test-only-not-a-real-key";

    private static LlmConfiguration Create(string endpoint = "https://provider.example/v1/messages",
        string model = "test-model", string authMode = "x-api-key", string apiKey = TestKey,
        LlmRequestConfiguration? request = null, LlmRuntimeConfiguration? runtime = null) => new()
    {
        Endpoint = endpoint,
        Model = model,
        AuthMode = authMode,
        ApiKey = apiKey,
        Request = request ?? new(),
        Runtime = runtime ?? new()
    };

    [TestMethod]
    public void ValidAnthropicSettingsPassWithoutNetwork()
    {
        var config = Create();
        Assert.AreEqual(10, config.Runtime!.MaxConcurrentRequests);
        Assert.IsEmpty(config.Validate(_ => null));
    }

    [TestMethod]
    public void BlankTemplateRequiresEndpointModelAndCredential()
    {
        var errors = new LlmConfiguration().Validate(_ => null);
        Assert.HasCount(3, errors);
    }

    [TestMethod]
    public void EnvironmentCredentialTakesPrecedence()
    {
        var config = Create();
        Assert.AreEqual("test-env-key", config.ResolveApiKey(_ => "test-env-key"));
        Assert.AreEqual(TestKey, config.ResolveApiKey(_ => ""));
    }

    [TestMethod]
    [DataRow("http://provider.example/v1/messages")]
    [DataRow("https://user:password@provider.example/v1/messages")]
    [DataRow("https://provider.example/v1/messages?key=test")]
    [DataRow("https://provider.example/v1/messages#fragment")]
    [DataRow("/v1/messages")]
    public void UnsafeOrRelativeEndpointsFail(string endpoint)
    {
        Assert.IsTrue(Create(endpoint: endpoint).Validate(_ => null)
            .Any(error => error.StartsWith("endpoint", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LocalUnauthenticatedEndpointIsExplicit()
    {
        Assert.IsEmpty(Create("http://127.0.0.1:8000/v1/messages", authMode: "none", apiKey: "")
            .Validate(_ => null));
        Assert.IsNotEmpty(Create(authMode: "none", apiKey: "").Validate(_ => null));
        Assert.IsNotEmpty(Create("http://localhost:8000/v1/messages", authMode: "none")
            .Validate(_ => null));
    }

    [TestMethod]
    public void InvalidRequestConcurrencyAndFailurePolicyAreRejected()
    {
        var config = Create(request: new() { TimeoutSeconds = 0, MaxOutputTokens = 0, Temperature = 2 },
            runtime: new() { MaxConcurrentRequests = 0, MaxRequestsPerRun = 2, FailurePolicy = "rules" });
        Assert.HasCount(5, config.Validate(_ => null));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(-1)]
    public void LegacyBudgetIsIgnoredAndDoesNotRestrictConcurrency(int legacyBudget)
    {
        Assert.IsEmpty(Create(runtime: new() { MaxConcurrentRequests = 3, MaxRequestsPerRun = legacyBudget }).Validate(_ => null));
    }

    [TestMethod]
    public void TabPrefixedModelIsRejected()
    {
        Assert.IsTrue(Create(model: "\ttest-model").Validate(_ => null)
            .Any(error => error.StartsWith("model", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DiagnosticsNeverIncludeCredential()
    {
        var config = Create(apiKey: TestKey + "\n");
        var errors = string.Join(" ", config.Validate(_ => null));
        Assert.IsFalse(errors.Contains(TestKey, StringComparison.Ordinal));
        Assert.IsFalse(config.ToString()!.Contains(TestKey, StringComparison.Ordinal));
    }

    [TestMethod]
    public void NullOptionSectionsFailCleanly()
    {
        var config = new LlmConfiguration
        {
            Endpoint = "https://provider.example/v1/messages", Model = "test-model", ApiKey = TestKey,
            Request = null, Runtime = null
        };
        Assert.HasCount(2, config.Validate(_ => null));
    }
}
