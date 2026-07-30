namespace NSchema.Model;

/// <summary>
/// The address of an object the database owns directly.
/// </summary>
/// <param name="Name">The object's name.</param>
/// <param name="Kind">The object's kind. Schemas and extensions have separate name spaces, so the kind is
/// part of the address rather than a refinement of it.</param>
public sealed record DatabaseAddress(SqlIdentifier Name, DatabaseObjectKind Kind) : Address
{
    /// <summary>
    /// The address of the named schema.
    /// </summary>
    /// <param name="name">The schema's name.</param>
    public static DatabaseAddress Schema(SqlIdentifier name) => new(name, DatabaseObjectKind.Schema);

    /// <summary>
    /// The address of the named extension.
    /// </summary>
    /// <param name="name">The extension's name.</param>
    public static DatabaseAddress Extension(SqlIdentifier name) => new(name, DatabaseObjectKind.Extension);

    /// <inheritdoc />
    protected override IReadOnlyList<SqlIdentifier> Path => [Name];

    // Only a schema holds objects; an extension is a leaf.
    /// <inheritdoc />
    protected override bool CanContain => Kind == DatabaseObjectKind.Schema;

    /// <inheritdoc />
    protected override bool NamesSameKindAs(Address other) => other is DatabaseAddress d && d.Kind == Kind;
}
