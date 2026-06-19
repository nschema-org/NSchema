using NSchema.Configuration.Ddl;
using NSchema.Schema.Ddl;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a local-file state store.
/// </summary>
internal sealed class FileStateConfig
{
    /// <summary>
    /// The path to the state file.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Maps a <c>BACKEND file</c> block's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static FileStateConfig FromBlock(ConfigBlock block)
    {
        var config = new FileStateConfig();
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "path":
                    config.Path = value.AsString();
                    break;
                default:
                    throw block.UnknownAttribute(key);
            }
        }

        return config;
    }
}
