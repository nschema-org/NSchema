using NSchema.Model;

namespace NSchema.Operations.Workflow;

/// <summary>
/// The outcome of capturing the live schema to the state store.
/// </summary>
/// <param name="Schema">The live schema written to the state store.</param>
/// <param name="SnapshotBytes">The size, in bytes, of the serialized snapshot.</param>
internal sealed record StateCapture(Database Schema, int SnapshotBytes);
