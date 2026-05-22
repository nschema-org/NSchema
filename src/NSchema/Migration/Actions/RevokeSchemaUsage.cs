namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the revocation of usage permissions on a schema from a specific role in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema for which usage permissions are being revoked.</param>
/// <param name="Role">The name of the role from which usage permissions are being revoked.</param>
public sealed record RevokeSchemaUsage(string SchemaName, string Role) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
