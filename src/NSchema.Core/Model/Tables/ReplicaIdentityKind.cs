namespace NSchema.Model.Tables;

/// <summary>
/// Defines how a <see cref="ReplicaIdentity"/> identifies a row.
/// </summary>
public enum ReplicaIdentityKind
{
    /// <summary>
    /// By every column.
    /// </summary>
    Full,

    /// <summary>
    /// By nothing.
    /// </summary>
    Nothing,

    /// <summary>
    /// By a unique index.
    /// </summary>
    Index,
}
