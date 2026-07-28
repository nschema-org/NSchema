namespace NSchema.State.Locks.Plugins;

/// <summary>
/// Options for configuring a <see cref="FileStateLock"/> instance.
/// </summary>
internal class FileStateLockOptions
{
    /// <summary>
    /// The absolute or relative path of the lock file. Acquiring creates it; releasing deletes it.
    /// </summary>
    public required string Path { get; set; }
}
