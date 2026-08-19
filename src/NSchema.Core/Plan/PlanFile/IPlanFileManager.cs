namespace NSchema.Plan.PlanFile;

/// <summary>
/// Reads and writes saved plan files.
/// </summary>
public interface IPlanFileManager
{
    /// <summary>
    /// Reads the envelope from the given file.
    /// </summary>
    Task<Result<PlanFileEnvelope>> Read(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the plan to the given file.
    /// </summary>
    Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken);
}
