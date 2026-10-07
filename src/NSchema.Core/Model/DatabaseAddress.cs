namespace NSchema.Model;

/// <summary>
/// The address of an object the database owns directly.
/// </summary>
/// <param name="Name">The object's name.</param>
/// <param name="Kind">The object's kind. Each kind has its own name space, so the kind is part of the address.</param>
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

    /// <summary>
    /// The address of the named publication.
    /// </summary>
    /// <param name="name">The publication's name.</param>
    public static DatabaseAddress Publication(SqlIdentifier name) => new(name, DatabaseObjectKind.Publication);

    /// <inheritdoc />
    protected override IReadOnlyList<SqlIdentifier> Path => [Name];

    // Only a schema holds objects; everything else is a leaf.
    /// <inheritdoc />
    protected override bool CanContain => Kind == DatabaseObjectKind.Schema;

    /// <inheritdoc />
    protected override bool NamesSameKindAs(Address other) => other is DatabaseAddress d && d.Kind == Kind;
}
