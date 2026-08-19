namespace NSchema.Model.Triggers;

/// <summary>
/// Whether a trigger fires once per affected row or once per statement.
/// </summary>
public enum TriggerLevel
{
    /// <summary>
    /// Fires once for each row affected by the statement.
    /// </summary>
    Row,

    /// <summary>
    /// Fires once per statement, regardless of the number of rows affected.
    /// </summary>
    Statement
}
