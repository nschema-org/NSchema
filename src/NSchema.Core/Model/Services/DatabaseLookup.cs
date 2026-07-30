using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Model.Services;

/// <summary>
/// Address → node lookup over a pure schema tree.
/// </summary>
internal sealed class DatabaseLookup(Database schema)
{
    private readonly Dictionary<SqlIdentifier, Schema> _schemas =
        schema.Schemas.ToDictionary(s => s.Name);

    public Schema? FindSchema(SqlIdentifier name) => _schemas.GetValueOrDefault(name);

    public Table? FindTable(ObjectAddress address) =>
        FindSchema(address.Schema)?.Tables.FirstOrDefault(t => t.Name == address.Name);

    /// <summary>
    /// Whether an object of <paramref name="kind"/> is declared at <paramref name="address"/>.
    /// </summary>
    public bool Has(SchemaObjectKind kind, ObjectAddress address) => kind switch
    {
        SchemaObjectKind.Table => FindTable(address) is not null,
        SchemaObjectKind.View => FindSchema(address.Schema)?.Views.Any(v => v.Name == address.Name) == true,
        SchemaObjectKind.Enum => FindSchema(address.Schema)?.Enums.Any(e => e.Name == address.Name) == true,
        SchemaObjectKind.Sequence => FindSchema(address.Schema)?.Sequences.Any(s => s.Name == address.Name) == true,
        SchemaObjectKind.Routine => FindSchema(address.Schema)?.Routines.Any(r => r.Name == address.Name) == true,
        SchemaObjectKind.Domain => FindSchema(address.Schema)?.Domains.Any(d => d.Name == address.Name) == true,
        SchemaObjectKind.CompositeType => FindSchema(address.Schema)?.CompositeTypes.Any(t => t.Name == address.Name) == true,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
