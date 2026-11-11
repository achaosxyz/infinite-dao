using InfiniteDao.Simulator.Configuration;

namespace InfiniteDao.Simulator;

public static class SimulatorCli
{
    public static int Run(string[] args, TextWriter output, TextWriter error,
        Func<string, string?>? readEnvironment = null)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "--help" or "-h"))
        {
            output.WriteLine("Infinite Dao - world simulator development bootstrap");
            output.WriteLine("check-config [--config <path>]  Validate local Anthropic settings without network access.");
            output.WriteLine("World evolution commands are not implemented yet.");
            return 0;
        }

        if (args[0] != "check-config"
            || (args.Length != 1 && !(args.Length == 3 && args[1] == "--config")))
        {
            error.WriteLine("Invalid arguments. Use --help for available commands.");
            return 64;
        }

        var path = args.Length == 3 ? args[2] : Path.Combine("config", "llm.local.json");
        try
        {
            var config = LlmConfigurationLoader.Load(path);
            var errors = config.Validate(readEnvironment ?? Environment.GetEnvironmentVariable);
            if (errors.Count > 0)
            {
                foreach (var issue in errors)
                    error.WriteLine(issue);
                return 2;
            }

            output.WriteLine("Anthropic configuration is locally valid. No network request was sent; provider connectivity is unverified.");
            return 0;
        }
        catch (ConfigurationException exception)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }
}
