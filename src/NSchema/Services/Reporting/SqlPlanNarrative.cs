using NSchema.Plan.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// The wording the text faces share when presenting a SQL plan, so the Spectre and Markdown renderings agree.
/// </summary>
internal static class SqlPlanNarrative
{
    /// <summary>
    /// The numbered comment above a statement, flagging any that runs outside the migration transaction.
    /// </summary>
    public static string Header(int index, int count, SqlStatement statement) =>
        $"-- [{index + 1}/{count}]{(statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty)}";
}
