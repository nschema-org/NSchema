namespace NSchema.Cli.Configuration;

/// <summary>
/// State-store configuration.
/// </summary>
internal sealed class StateConfig
{
    /// <summary>Path to the local file the schema state is persisted to and read from.</summary>
    public string? File { get; set; }
}
