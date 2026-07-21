using NSchema.Plugins.Model.Config;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a local-file state store.
/// </summary>
internal sealed class FileStateConfig
{
    private static readonly AttributeKey _pathKey = new("path");

    /// <summary>
    /// The path to the state file.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Maps a <c>STATE file</c> statement's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static FileStateConfig FromConfig(PluginConfig config)
    {
        var parsed = new FileStateConfig();
        foreach (var (key, value) in config.Attributes)
        {
            if (key == _pathKey)
            {
                parsed.Path = value.AsString();
            }
            else
            {
                throw new InvalidOperationException($"Unknown attribute '{key}' in the STATE file statement.");
            }
        }

        return parsed;
    }
}
