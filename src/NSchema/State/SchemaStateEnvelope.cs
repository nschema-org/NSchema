using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// The versioned wrapper persisted by the state store.
/// </summary>
/// <param name="Version">The version of the envelope format.</param>
/// <param name="Schema">The captured database schema.</param>
internal sealed record SchemaStateEnvelope(int Version, DatabaseSchema Schema)
{
    /// <summary>The current envelope format version.</summary>
    public const int CurrentVersion = 1;
}
