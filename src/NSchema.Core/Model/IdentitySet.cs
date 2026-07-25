using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// A set of database identities, structured by the level they live at: schema containers, schema-level
/// objects, and database-global extensions. Membership is by value.
/// </summary>
public sealed record IdentitySet(
    IReadOnlyList<SchemaAddress>? Schemas = null,
    IReadOnlyList<ObjectAddress>? Objects = null,
    IReadOnlyList<ScopedAddress>? Extensions = null
)
{
    /// <summary>
    /// The set containing no identities.
    /// </summary>
    public static IdentitySet Empty { get; } = new();

    /// <summary>
    /// The schemas in the set.
    /// </summary>
    public IReadOnlyList<SchemaAddress> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// The schema-level object identities in the set.
    /// </summary>
    public IReadOnlyList<ObjectAddress> Objects { get; init; } = Objects ?? [];

    /// <summary>
    /// The extensions in the set.
    /// </summary>
    public IReadOnlyList<ScopedAddress> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// Whether the set contains no identities.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Schemas.Count == 0 && Objects.Count == 0 && Extensions.Count == 0;

    /// <summary>
    /// Whether the named schema is in the set.
    /// </summary>
    public bool ContainsSchema(SqlIdentifier name) => Schemas.Contains(new SchemaAddress(name));

    /// <summary>
    /// Whether the object identity is in the set.
    /// </summary>
    public bool ContainsObject(ObjectAddress address) => Objects.Contains(address);

    /// <summary>
    /// Whether the object is in the set: schema-scoped objects match by identity, global ones by name.
    /// </summary>
    public bool Contains(DatabaseObject obj) =>
        obj.Address is ObjectAddress identity ? ContainsObject(identity) : ContainsExtension(obj.Name);

    /// <summary>
    /// Whether the named extension is in the set.
    /// </summary>
    public bool ContainsExtension(SqlIdentifier name) => Extensions.Contains(new ScopedAddress(null, name));

    /// <summary>
    /// The set containing every identity in either set.
    /// </summary>
    public IdentitySet Union(IdentitySet other) => new(
        [.. Schemas.Union(other.Schemas)],
        [.. Objects.Union(other.Objects)],
        [.. Extensions.Union(other.Extensions)]);

    /// <summary>
    /// The subset of identities the scope covers.
    /// </summary>
    public IdentitySet CoveredBy(PlanningScope scope) => scope.IsUnscoped ? this : new IdentitySet(
        [.. Schemas.Where(scope.Contains)],
        [.. Objects.Where(scope.Contains)],
        Extensions
    );

    /// <summary>
    /// The set containing this set's identities without those in <paramref name="other"/>.
    /// </summary>
    public IdentitySet Except(IdentitySet other) => new(
        [.. Schemas.Except(other.Schemas)],
        [.. Objects.Except(other.Objects)],
        [.. Extensions.Except(other.Extensions)]);
}
