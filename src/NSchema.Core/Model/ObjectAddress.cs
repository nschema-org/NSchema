namespace NSchema.Model;

/// <summary>
/// The address of a schema-level object.
/// </summary>
/// <param name="Schema">The schema containing the object.</param>
/// <param name="Name">The object's name within that schema.</param>
/// <param name="Kind">The object's kind, or <see langword="null"/> to address every kind sharing the name.</param>
public sealed record ObjectAddress(SqlIdentifier Schema, SqlIdentifier Name, SchemaObjectKind? Kind = null) : Address
{
    /// <summary>
    /// The address of the named table.
    /// </summary>
    /// <param name="schema">The schema containing the table.</param>
    /// <param name="name">The table's name.</param>
    public static ObjectAddress Table(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.Table);

    /// <summary>
    /// The address of the named view.
    /// </summary>
    /// <param name="schema">The schema containing the view.</param>
    /// <param name="name">The view's name.</param>
    public static ObjectAddress View(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.View);

    /// <summary>
    /// The address of the named enum type.
    /// </summary>
    /// <param name="schema">The schema containing the enum.</param>
    /// <param name="name">The enum's name.</param>
    public static ObjectAddress Enum(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.Enum);

    /// <summary>
    /// The address of the named sequence.
    /// </summary>
    /// <param name="schema">The schema containing the sequence.</param>
    /// <param name="name">The sequence's name.</param>
    public static ObjectAddress Sequence(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.Sequence);

    /// <summary>
    /// The address of the named routine.
    /// </summary>
    /// <param name="schema">The schema containing the routine.</param>
    /// <param name="name">The routine's name.</param>
    public static ObjectAddress Routine(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.Routine);

    /// <summary>
    /// The address of the named domain.
    /// </summary>
    /// <param name="schema">The schema containing the domain.</param>
    /// <param name="name">The domain's name.</param>
    public static ObjectAddress Domain(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.Domain);

    /// <summary>
    /// The address of the named composite type.
    /// </summary>
    /// <param name="schema">The schema containing the composite type.</param>
    /// <param name="name">The composite type's name.</param>
    public static ObjectAddress CompositeType(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.CompositeType);

    /// <summary>
    /// The address of the named native type.
    /// </summary>
    /// <param name="schema">The schema containing the native type.</param>
    /// <param name="name">The native type's name.</param>
    public static ObjectAddress NativeType(SqlIdentifier schema, SqlIdentifier name) =>
        new(schema, name, SchemaObjectKind.NativeType);

    /// <inheritdoc />
    protected override IReadOnlyList<SqlIdentifier> Path => [Schema, Name];

    /// <inheritdoc />
    protected override bool CanContain => true;

    // A kind-free address names every kind at the location, so it covers all of them.
    /// <inheritdoc />
    protected override bool NamesSameKindAs(Address other) =>
        other is ObjectAddress o && (Kind is null || Kind == o.Kind);

    /// <summary>
    /// The address of one of the object's members.
    /// </summary>
    /// <param name="member">The member's name within the object.</param>
    /// <param name="kind">The member's kind, or <see langword="null"/> to address every kind sharing the name.</param>
    public MemberAddress Member(SqlIdentifier member, MemberKind? kind = null) => new(Schema, Name, member, kind);
}
