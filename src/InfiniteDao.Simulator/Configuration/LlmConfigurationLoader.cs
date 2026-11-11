using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniteDao.Agents.Configuration;

namespace InfiniteDao.Simulator.Configuration;

public static class LlmConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    public static LlmConfiguration Load(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            return JsonSerializer.Deserialize<LlmConfiguration>(file, Options)
                ?? throw new ConfigurationException("Configuration must be a JSON object.");
        }
        catch (JsonException)
        {
            // Do not forward JSON parser messages: property names/values may contain secrets.
            throw new ConfigurationException("Configuration contains invalid JSON, duplicate/unknown fields or incorrect value types.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                     or ArgumentException or NotSupportedException)
        {
            throw new ConfigurationException("Configuration file could not be read. Check --config and file permissions.");
        }
    }
}

public sealed class ConfigurationException(string message) : Exception(message);
