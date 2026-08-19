namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// A change-event trigger.
/// </summary>
public enum ChangeTrigger
{
    /// <summary>
    /// <c>ADD COLUMN</c>.
    /// </summary>
    AddColumn,

    /// <summary>
    /// <c>ALTER COLUMN TYPE</c>.
    /// </summary>
    AlterColumnType,

    /// <summary>
    /// <c>ADD CONSTRAINT</c>.
    /// </summary>
    AddConstraint
}
