using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents changing what identifies a table's rows to a publication's subscribers.
/// </summary>
/// <param name="Table">The table.</param>
/// <param name="OldIdentity">The replica identity before the change, or <see langword="null"/> for the engine's default.</param>
/// <param name="NewIdentity">The replica identity after the change, or <see langword="null"/> for the engine's default.</param>
public sealed record SetReplicaIdentity(ObjectAddress Table, ReplicaIdentity? OldIdentity, ReplicaIdentity? NewIdentity) : MigrationAction;
