using NSchema.Configuration;
using NSchema.Model;
using NSchema.Project.Nsql;

namespace NSchema.Extensions;

/// <summary>
/// Maps the CLI's <c>--scope</c> values onto the core's <see cref="PlanningScope"/>.
/// </summary>
internal static class ScopeExtensions
{
    extension(string[]? scope)
    {
        /// <summary>
        /// The planning scope covering the addressed schemas and objects, or <see cref="PlanningScope.All"/>
        /// when none are given. Each value is an address — <c>app</c> or <c>app.orders</c> — read under the
        /// NSQL identifier rules, so a quoted segment can carry the dots and spaces a bare name cannot.
        /// </summary>
        public Result<PlanningScope> ToPlanningScope()
        {
            if (scope is not { Length: > 0 })
            {
                return PlanningScope.All;
            }

            List<Diagnostic> diagnostics = [];
            List<Address> addresses = [];
            foreach (var value in scope)
            {
                var read = NsqlReader.ReadAddress(value);
                if (read.Value is not { } address)
                {
                    var reason = read.Errors.FirstOrDefault()?.Message ?? "it is not an address";
                    diagnostics.Add(OptionDiagnostics.InvalidScope(value, reason));
                    continue;
                }

                // The scope model addresses schemas and objects; a member is a level below what a run can target.
                if (address is MemberAddress member)
                {
                    diagnostics.Add(OptionDiagnostics.UnsupportedScopeTarget(value, member.Owner));
                    continue;
                }

                addresses.Add(address);
            }

            return diagnostics.Count > 0 ? Result.Failure<PlanningScope>(diagnostics) : PlanningScope.To(addresses);
        }
    }
}
