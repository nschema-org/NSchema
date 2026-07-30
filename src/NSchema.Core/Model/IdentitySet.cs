using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// A set of database identities, structured by the level they live at.
/// </summary>
public sealed record IdentitySet(IReadOnlyList<DatabaseAddress>? DatabaseObjects = null, IReadOnlyList<ObjectAddress>? SchemaObjects = null)
{
    /// <summary>
    /// The set containing no identities.
    /// </summary>
    public static IdentitySet Empty { get; } = new();

    /// <summary>
    /// The database-level identities in the set.
    /// </summary>
    public IReadOnlyList<DatabaseAddress> DatabaseObjects { get; init; } = DatabaseObjects ?? [];

    /// <summary>
    /// The schema-level object identities in the set.
    /// </summary>
    public IReadOnlyList<ObjectAddress> SchemaObjects { get; init; } = SchemaObjects ?? [];

    /// <summary>
    /// Whether the set contains no identities.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => DatabaseObjects.Count == 0 && SchemaObjects.Count == 0;

    /// <summary>
    /// The schemas in the set.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<DatabaseAddress> Schemas => DatabaseObjects.Where(o => o.Kind == DatabaseObjectKind.Schema);

    /// <summary>
    /// The extensions in the set.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<DatabaseAddress> Extensions => DatabaseObjects.Where(o => o.Kind == DatabaseObjectKind.Extension);

    /// <summary>
    /// Whether the named schema is in the set.
    /// </summary>
    public bool ContainsSchema(SqlIdentifier name) => DatabaseObjects.Contains(DatabaseAddress.Schema(name));

    /// <summary>
    /// Whether the named extension is in the set.
    /// </summary>
    public bool ContainsExtension(SqlIdentifier name) => DatabaseObjects.Contains(DatabaseAddress.Extension(name));

    /// <summary>
    /// Whether the object identity is in the set.
    /// </summary>
    public bool ContainsObject(ObjectAddress address) => SchemaObjects.Contains(address);

    /// <summary>
    /// Whether the object is in the set.
    /// </summary>
    public bool Contains(SchemaObject obj) => ContainsObject(obj.Address);

    /// <summary>
    /// The set containing every identity in either set.
    /// </summary>
    public IdentitySet Union(IdentitySet other) => new(
        [.. DatabaseObjects.Union(other.DatabaseObjects)],
        [.. SchemaObjects.Union(other.SchemaObjects)]);

    /// <summary>
    /// The subset of identities the scope covers.
    /// </summary>
    public IdentitySet CoveredBy(PlanningScope scope) => scope.IsUnscoped ? this : new IdentitySet(
        // Nothing contains an extension, so no schema scope excludes one.
        [.. DatabaseObjects.Where(o => o.Kind != DatabaseObjectKind.Schema || scope.Contains(o))],
        [.. SchemaObjects.Where(scope.Contains)]);

    /// <summary>
    /// The set containing this set's identities without those in <paramref name="other"/>.
    /// </summary>
    public IdentitySet Except(IdentitySet other) => new(
        [.. DatabaseObjects.Except(other.DatabaseObjects)],
        [.. SchemaObjects.Except(other.SchemaObjects)]);
}
