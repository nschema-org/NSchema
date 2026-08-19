using NSchema.Model;

namespace NSchema.Deployment;

/// <summary>
/// Provides the live database.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>
    /// Reads the live database, restricted to <paramref name="scope"/>.
    /// The scope is a fetch hint to the introspector, which may over-return.
    /// </summary>
    /// <param name="scope">The schemas to include.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<Database>> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default);
}
