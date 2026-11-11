using InfiniteDao.Simulator;

namespace InfiniteDao.IntegrationTests;

[TestClass]
public sealed class ConfigurationCliTests
{
    private const string ValidJson = """
        {
          "protocol": "anthropic-messages",
          "endpoint": "https://provider.example/v1/messages",
          "model": "test-model",
          "apiKey": "test-only-secret",
          "anthropicVersion": "2023-06-01"
        }
        """;

    [TestMethod]
    public void ValidConfigurationPassesOfflineAndDoesNotPrintValues()
    {
        using var file = new TemporaryConfig(ValidJson);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = SimulatorCli.Run(["check-config", "--config", file.Path], output, error, _ => null);
        Assert.AreEqual(0, code);
        Assert.AreEqual("", error.ToString());
        Assert.IsTrue(output.ToString().Contains("No network request", StringComparison.Ordinal));
        Assert.IsFalse(output.ToString().Contains("test-only-secret", StringComparison.Ordinal));
        Assert.IsFalse(output.ToString().Contains("provider.example", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("{\"apiKey\": \"test-only-secret\",")]
    [DataRow("{\"test-only-secret\": 1}")]
    [DataRow("{\"model\": \"a\", \"model\": \"test-only-secret\"}")]
    [DataRow("{\"request\": {\"timeoutSeconds\": \"test-only-secret\"}}")]
    [DataRow("null")]
    public void MalformedUnknownAndDuplicateFieldsFailWithoutEchoingInput(string json)
    {
        using var file = new TemporaryConfig(json);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.AreEqual(2, SimulatorCli.Run(["check-config", "--config", file.Path], output, error, _ => null));
        Assert.IsNotEmpty(error.ToString());
        Assert.IsFalse(error.ToString().Contains("test-only-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MissingFileReportsSafeError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.AreEqual(2, SimulatorCli.Run(["check-config", "--config", missing], output, error, _ => null));
        Assert.IsFalse(error.ToString().Contains(missing, StringComparison.Ordinal));
    }

    [TestMethod]
    public void HelpDoesNotPretendWorldSimulationExists()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.AreEqual(0, SimulatorCli.Run([], output, error, _ => null));
        Assert.IsTrue(output.ToString().Contains("not implemented", StringComparison.Ordinal));
        Assert.AreEqual(64, SimulatorCli.Run(["simulate"], output, error, _ => null));
    }

    private sealed class TemporaryConfig : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "infinite-dao-test-" + Guid.NewGuid().ToString("N") + ".json");

        public TemporaryConfig(string json) => File.WriteAllText(Path, json);
        public void Dispose() => File.Delete(Path);
    }
}
