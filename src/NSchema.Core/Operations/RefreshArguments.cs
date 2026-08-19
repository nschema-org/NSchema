namespace NSchema.Operations;

/// <summary>
/// Arguments for a refresh run.
/// </summary>
public sealed record RefreshArguments
{
    /// <summary>
    /// When true, an existing state payload that cannot be read is replaced instead of failing the refresh.
    /// </summary>
    public bool Force { get; init; }
}
