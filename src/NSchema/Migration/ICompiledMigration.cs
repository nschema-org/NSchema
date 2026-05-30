using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// A migration plan compiled into an executable unit of work.
/// </summary>
public interface ICompiledMigration
{
    /// <summary>
    /// The migration plan this unit was compiled from.
    /// </summary>
    MigrationPlan Plan { get; }

    /// <summary>
    /// A human-readable representation of what executing this unit would do.
    /// </summary>
    IReadOnlyList<string> Preview { get; }

    /// <summary>
    /// Runs the compiled migration against the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(CancellationToken cancellationToken = default);
}
