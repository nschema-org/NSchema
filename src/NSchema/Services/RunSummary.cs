using NSchema.Diff.Model;
using NSchema.Plan.Model;

namespace NSchema.Services;

/// <summary>
/// Formats the one-line recap a command shows when an operation finishes.
/// </summary>
internal static class RunSummary
{
    /// <summary>
    /// Describes the changes in <paramref name="diff"/>.
    /// </summary>
    public static string Describe(DatabaseDiff diff)
    {
        var (added, modified, removed) = diff.GetSummary();

        var changes = new List<string>(3);
        if (added > 0)
        {
            changes.Add($"{added} added");
        }

        if (modified > 0)
        {
            changes.Add($"{modified} changed");
        }

        if (removed > 0)
        {
            changes.Add($"{removed} destroyed");
        }

        return changes.Count > 0 ? string.Join(", ", changes) : "no changes";
    }

    /// <summary>
    /// Describes the changes in <paramref name="plan"/> together with the number of SQL statements that ran.
    /// </summary>
    public static string Describe(MigrationPlan plan)
    {
        var count = plan.Statements.Count;
        var statements = count == 1 ? "1 statement" : $"{count} statements";
        return $"{Describe(plan.Diff)} ({statements})";
    }
}
