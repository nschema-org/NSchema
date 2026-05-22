namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the removal of an existing schema from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema to be removed from the database schema.</param>
/// <remarks>
/// This action is considered destructive because it may result in data loss if the schema contains tables or other objects.
/// </remarks>
public sealed record DropSchema(string SchemaName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
