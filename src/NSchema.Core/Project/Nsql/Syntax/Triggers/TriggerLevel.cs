namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// The <c>FOR EACH</c> level of a trigger.
/// </summary>
public enum TriggerLevel
{
    /// <summary>
    /// <c>FOR EACH ROW</c>.
    /// </summary>
    Row,

    /// <summary>
    /// <c>FOR EACH STATEMENT</c> (the default when unwritten).
    /// </summary>
    Statement
}
