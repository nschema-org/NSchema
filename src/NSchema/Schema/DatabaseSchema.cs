using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents the overall structure of a database schema.
/// </summary>
/// <param name="Schemas">A list of SchemaDefinition objects, each representing a specific schema within the database.</param>
/// <param name="DroppedSchemas">A list of schema names that have been dropped from the database.</param>
[DebuggerDisplay("{Schemas.Count} schemas")]
public record DatabaseSchema(
    IReadOnlyList<SchemaDefinition> Schemas,
    IReadOnlyList<string> DroppedSchemas
);
