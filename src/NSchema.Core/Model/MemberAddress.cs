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
    /// <summary>
    /// The address of the named column.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the column.</param>
    /// <param name="name">The column's name.</param>
    public static MemberAddress Column(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.Column);

    /// <summary>
    /// The address of the named primary key.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the primary key.</param>
    /// <param name="name">The primary key's name.</param>
    public static MemberAddress PrimaryKey(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.PrimaryKey);

    /// <summary>
    /// The address of the named foreign key.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the foreign key.</param>
    /// <param name="name">The foreign key's name.</param>
    public static MemberAddress ForeignKey(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.ForeignKey);

    /// <summary>
    /// The address of the named unique constraint.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the unique constraint.</param>
    /// <param name="name">The unique constraint's name.</param>
    public static MemberAddress UniqueConstraint(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.UniqueConstraint);

    /// <summary>
    /// The address of the named check constraint.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the check constraint.</param>
    /// <param name="name">The check constraint's name.</param>
    public static MemberAddress CheckConstraint(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.CheckConstraint);

    /// <summary>
    /// The address of the named exclusion constraint.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the exclusion constraint.</param>
    /// <param name="name">The exclusion constraint's name.</param>
    public static MemberAddress ExclusionConstraint(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.ExclusionConstraint);

    /// <summary>
    /// The address of the named index.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the index.</param>
    /// <param name="name">The index's name.</param>
    public static MemberAddress Index(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.Index);

    /// <summary>
    /// The address of the named trigger.
    /// </summary>
    /// <param name="schema">The schema containing the owning object.</param>
    /// <param name="object">The object that owns the trigger.</param>
    /// <param name="name">The trigger's name.</param>
    public static MemberAddress Trigger(SqlIdentifier schema, SqlIdentifier @object, SqlIdentifier name) =>
        new(schema, @object, name, MemberKind.Trigger);

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
