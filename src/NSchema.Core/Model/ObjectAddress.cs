namespace NSchema.Model;

/// <summary>
/// The address of a schema-level object.
/// </summary>
/// <param name="Schema">The schema containing the object.</param>
/// <param name="Name">The object's name within that schema.</param>
/// <param name="Kind">The object's kind, or <see langword="null"/> to address every kind sharing the name.</param>
public sealed record ObjectAddress(SqlIdentifier Schema, SqlIdentifier Name, ObjectKind? Kind = null) : Address
{
    /// <inheritdoc />
    public override string Value => $"{Schema}.{Name}";

    /// <inheritdoc />
    public override SqlIdentifier SchemaName => Schema;

    /// <inheritdoc />
    public override bool Covers(Address other) => other switch
    {
        // A kind-free address covers every kind at the location; members are covered by owner alone.
        ObjectAddress o => o.Schema == Schema && o.Name == Name && (Kind is null || Kind == o.Kind),
        MemberAddress m => m.Schema == Schema && m.Object == Name,
        _ => false,
    };

    /// <summary>
    /// The address of one of the object's members.
    /// </summary>
    /// <param name="member">The member's name within the object.</param>
    public MemberAddress Member(SqlIdentifier member) => new(Schema, Name, member);
}
