using NSchema.Model;

namespace NSchema.Deployment.Plugins;

/// <summary>
/// Introspects the live database into the schema model.
/// </summary>
public interface IDatabaseIntrospector
{
    /// <summary>
    /// Reads the live schema inside <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The schemas to include.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema for the scoped schema names.</returns>
    /// <remarks>
    /// The scope is an optimization hint: an introspector may over-return (read more than the scope), and the
    /// engine re-applies the scope after every read, so scoping semantics stay single-sourced. A schema name
    /// that does not exist is simply not included in the returned schema.
    /// </remarks>
    ValueTask<Result<Database>> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default);
}
