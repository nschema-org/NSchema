namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of an object's member.
/// </summary>
/// <param name="Schema">The schema containing the owning object.</param>
/// <param name="Object">The object that owns the member.</param>
/// <param name="Member">The member's name within that object.</param>
/// <param name="Kind">The member's kind, or <see langword="null"/> to address every kind sharing the name.</param>
public sealed record MemberAddress(
    SqlIdentifier Schema,
    SqlIdentifier Object,
    SqlIdentifier Member,
    MemberKind? Kind = null
) : Address
{
    /// <inheritdoc />
    protected override IReadOnlyList<SqlIdentifier> Path => [Schema, Object, Member];

    // Nothing lives inside a member.
    /// <inheritdoc />
    protected override bool CanContain => false;

    // A kind-free address names every kind at the location, so it covers all of them.
    /// <inheritdoc />
    protected override bool NamesSameKindAs(Address other) =>
        other is MemberAddress m && (Kind is null || Kind == m.Kind);

    /// <summary>
    /// The address of the object that owns the member.
    /// </summary>
    public ObjectAddress Owner => new(Schema, Object);
}
