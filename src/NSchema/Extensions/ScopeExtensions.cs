using NSchema.Model;

namespace NSchema.Extensions;

/// <summary>
/// Maps the CLI's <c>--scope</c> values onto the core's <see cref="PlanningScope"/>.
/// </summary>
internal static class ScopeExtensions
{
    extension(string[]? scope)
    {
        /// <summary>
        /// The planning scope covering the named schemas, or <see cref="PlanningScope.All"/> when none are named.
        /// </summary>
        public PlanningScope ToPlanningScope() => scope is { Length: > 0 }
            ? PlanningScope.To(scope.Select(s => new SqlIdentifier(s)))
            : PlanningScope.All;
    }
}
