namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// A script's <c>RUN</c> condition.
/// </summary>
public enum RunCondition
{
    /// <summary>
    /// <c>RUN ALWAYS</c> (the default).
    /// </summary>
    Always,

    /// <summary>
    /// <c>RUN ONCE</c> — ledger-enforced.
    /// </summary>
    Once
}
