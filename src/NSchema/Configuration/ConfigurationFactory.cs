using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration;

internal static class ConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    /// <summary>
    /// The serializer options used to read and write <c>nschema.json</c>.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static T LoadFromFile<T>(ParseResult result) where T : class, new()
    {
        var configRequired = CommonOptions.Config.TryGetValue(result, out var cliPath);
        var configFile = Path.GetFullPath(cliPath ?? DefaultConfigurationFile, Directory.GetCurrentDirectory());

        if (!File.Exists(configFile))
        {
            return configRequired
                ? throw new FileNotFoundException($"Config file not found: \"{configFile}\".", configFile)
                : new T();
        }

        using var stream = File.OpenRead(configFile);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to parse config file \"{configFile}\".");
    }


    public static T Load<T>(ParseResult args) where T : class, IBindable, new()
    {
        var config = LoadFromFile<T>(args);
        config.Bind(args);
        return config;
    }
}
