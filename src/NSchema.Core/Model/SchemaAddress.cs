namespace NSchema.Model;

/// <summary>
/// The address of a schema.
/// </summary>
/// <param name="Schema">The schema's name.</param>
public sealed record SchemaAddress(SqlIdentifier Schema) : Address
{
    /// <inheritdoc />
    public override string Value => Schema.Value;

    /// <inheritdoc />
    public override SqlIdentifier SchemaName => Schema;

    /// <inheritdoc />
    public override bool Covers(Address other) => other.SchemaName == Schema;
}
