using NSchema.State.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// How the recorded ledger reads wherever it is reported, so `script list` and the scripts riding a recorded state
/// present the same entries the same way.
/// </summary>
internal static class ScriptLedger
{
    /// <summary>
    /// The entries in the order they ran, which is the order they are reported in.
    /// </summary>
    public static IEnumerable<ScriptExecution> InExecutionOrder(IReadOnlyList<ScriptExecution> scripts) =>
        scripts.OrderBy(s => s.ExecutedUtc);

    /// <summary>
    /// What names the entry's script: `name`, or `schema.name` for a scoped one.
    /// </summary>
    public static string Name(ScriptExecution script) => script.Script.ToString();

    /// <summary>
    /// When the entry ran, stamped in UTC.
    /// </summary>
    public static string Executed(ScriptExecution script) => $"{script.ExecutedUtc:u}";
}
