using NSchema.Model;

namespace NSchema.Operations;

/// <summary>
/// The result of a refresh.
/// </summary>
/// <param name="Database">The live schema that was written to the state store.</param>
/// <param name="SnapshotBytes">The size, in bytes, of the serialized state snapshot.</param>
public sealed record RefreshResult(Database Database, int SnapshotBytes);
