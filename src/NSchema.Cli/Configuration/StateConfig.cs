namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig
{
    /// <summary>
    /// The type of store being used.
    /// </summary>
    public StateType? Type { get; set; }

    /// <summary>
    /// The connection string the selected type connects with.
    /// </summary>
    public string? ConnectionString { get; set; }
}
