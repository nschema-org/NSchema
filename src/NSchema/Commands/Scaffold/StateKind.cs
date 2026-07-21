namespace NSchema.Commands.Scaffold;

/// <summary>
/// The state backend a scaffolded project is configured for.
/// </summary>
internal enum StateKind
{
    /// <summary>
    /// A local state file.
    /// </summary>
    File,

    /// <summary>
    /// An Amazon S3 object.
    /// </summary>
    S3,
}
