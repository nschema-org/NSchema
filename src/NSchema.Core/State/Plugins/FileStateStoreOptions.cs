namespace NSchema.State.Plugins;

/// <summary>
/// Options for configuring a <see cref="FileDatabaseStateStore"/> instance.
/// </summary>
internal class FileDatabaseStateStoreOptions
{
    /// <summary>
    /// The absolute or relative path of the state file.
    /// </summary>
    public required string Path { get; set; }
}
