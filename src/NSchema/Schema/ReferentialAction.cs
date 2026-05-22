namespace NSchema.Schema;

/// <summary>
/// Defines the actions that can be taken on a foreign key constraint when the referenced row is updated or deleted.
/// </summary>
public enum ReferentialAction
{
    /// <summary>
    /// Indicates that no specific action is taken when the referenced row is updated or deleted.
    /// </summary>
    NoAction,

    /// <summary>
    /// Indicates that when the referenced row is updated or deleted, the corresponding rows in the referencing table are also updated or deleted (cascading effect).
    /// </summary>
    Cascade,

    /// <summary>
    /// Indicates that when the referenced row is updated or deleted, the corresponding foreign key values in the referencing table are set to NULL.
    /// </summary>
    SetNull,

    /// <summary>
    /// Indicates that when the referenced row is updated or deleted, the corresponding foreign key values in the referencing table are set to their default values.
    /// </summary>
    SetDefault,
}
