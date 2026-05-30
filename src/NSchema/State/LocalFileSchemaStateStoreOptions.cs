namespace NSchema.State;

/// <summary>
/// Options for configuring a <see cref="LocalFileSchemaStateStore"/> instance.
/// </summary>
public class LocalFileSchemaStateStoreOptions
{
    /// <summary>
    /// The absolute or relative path of the state file.
    /// </summary>
    public required string Path { get; set; }
}
