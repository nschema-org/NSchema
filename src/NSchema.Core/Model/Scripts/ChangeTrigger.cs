namespace NSchema.Model.Scripts;

/// <summary>
/// The structural change a change-event script attaches to.
/// </summary>
public enum ChangeTrigger
{
    /// <summary>
    /// A column is added to an existing table.
    /// </summary>
    AddColumn,

    /// <summary>
    /// An existing column's type changes.
    /// </summary>
    AlterColumnType,

    /// <summary>
    /// A constraint is added to an existing table.
    /// </summary>
    AddConstraint
}
