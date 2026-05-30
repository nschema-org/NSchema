namespace NSchema.State;

/// <summary>
/// Options for configuring a <see cref="FileSchemaStateStore"/> instance.
/// </summary>
public class FileSchemaStateStoreOptions
{
    /// <summary>
    /// The absolute or relative path of the state file.
    /// </summary>
    public required string Path { get; set; }
}
