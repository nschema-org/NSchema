namespace NSchema.Cli.Configuration.State;

/// <summary>
/// Configures a local-file state store.
/// </summary>
internal sealed class FileStateConfig
{
    /// <summary>
    /// The path to the state file.
    /// </summary>
    public string Path { get; set; } = "";
}
