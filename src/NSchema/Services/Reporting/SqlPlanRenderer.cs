using System.Text;
using NSchema.Sql.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders a <see cref="SqlPlan"/> into human-readable text for previewing.
/// </summary>
internal static class SqlPlanRenderer
{
    /// <summary>
    /// Renders the SQL plan as text.
    /// </summary>
    public static string Render(SqlPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SQL Preview:");

        if (plan.IsEmpty)
        {
            sb.Append("- No statements to execute");
            return sb.ToString();
        }

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            var statement = plan.Statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            sb.AppendLine($"-- [{i + 1}/{plan.Statements.Count}]{marker}");
            sb.AppendLine(statement.Sql);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
