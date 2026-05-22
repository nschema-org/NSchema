namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the creation of a new schema in the database.
/// </summary>
/// <param name="SchemaName">The name of the schema to be created.</param>
/// <remarks>
/// This action is used to define a new namespace for database objects, allowing for better organization and separation of concerns within the database schema.
/// </remarks>
public sealed record CreateSchema(string SchemaName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
