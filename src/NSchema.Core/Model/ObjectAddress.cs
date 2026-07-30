namespace NSchema.Model;

/// <summary>
/// The address of a schema-level object.
/// </summary>
/// <param name="Schema">The schema containing the object.</param>
/// <param name="Name">The object's name within that schema.</param>
/// <param name="Kind">The object's kind, or <see langword="null"/> to address every kind sharing the name.</param>
public sealed record ObjectAddress(SqlIdentifier Schema, SqlIdentifier Name, SchemaObjectKind? Kind = null) : Address
{
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
