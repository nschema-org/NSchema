using NSchema.Model.Publications;

namespace NSchema.Model.Tables;

/// <summary>
/// Defines what identifies a table's row to a subscriber when a <see cref="Publication"/> sends an update or a delete.
/// </summary>
/// <param name="Kind">How the row is identified.</param>
/// <param name="Index">The unique index identifying it, when <paramref name="Kind"/> is <see cref="ReplicaIdentityKind.Index"/>.</param>
public sealed record ReplicaIdentity(ReplicaIdentityKind Kind, SqlIdentifier? Index = null)
{
    /// <summary>
    /// The combination of every column forms a row's identity.
    /// </summary>
    public static ReplicaIdentity Full { get; } = new(ReplicaIdentityKind.Full);

    /// <summary>
    /// Rows have no identity.
    /// </summary>
    public static ReplicaIdentity Nothing { get; } = new(ReplicaIdentityKind.Nothing);

    /// <summary>
    /// Rows are identified based on the given index.
    /// </summary>
    public static ReplicaIdentity UsingIndex(SqlIdentifier index) => new(ReplicaIdentityKind.Index, index);
}
