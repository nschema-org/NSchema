namespace NSchema.Hosting;

/// <summary>
/// Runs the end-to-end migration: planning, user-facing reporting, and execution.
/// </summary>
internal interface IMigrationPipeline
{
    /// <summary>
    /// Computes and renders the migration plan without applying it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Plan(CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the migration plan and applies it to the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Apply(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live current schema and writes it to the state store, without planning or applying anything.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Refresh(CancellationToken cancellationToken = default);
}
