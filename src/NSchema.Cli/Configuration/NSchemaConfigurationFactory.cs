using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
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
        var cliPath = result.GetValue(CommonOptions.Config);
        var configFile = Path.GetFullPath(cliPath ?? DefaultConfigurationFile, Directory.GetCurrentDirectory());

        if (!File.Exists(configFile))
        {
            return cliPath is null
                ? new T()
                : throw new FileNotFoundException($"Config file not found: \"{configFile}\".", configFile);
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

internal interface IBindable
{
    void Bind(ParseResult result);
}
