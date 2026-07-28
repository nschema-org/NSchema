using NSchema.Deployment.Plugins;
using NSchema.Model;

namespace NSchema.Tests.Helpers;

/// <summary>
/// An <see cref="IDatabaseIntrospector"/> that returns a fixed in-memory schema (honoring scope filtering).
/// </summary>
internal sealed class InMemoryIntrospector(Database schema) : IDatabaseIntrospector
{
    public ValueTask<Result<Database>> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success(schema.ScopedTo(scope)));
}
