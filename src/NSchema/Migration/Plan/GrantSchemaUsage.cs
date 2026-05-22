namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the granting of USAGE privileges on a database schema to a specific role.
/// </summary>
/// <param name="SchemaName">The name of the schema on which USAGE privileges are being granted.</param>
/// <param name="Role">The name of the role to which USAGE privileges are being granted.</param>
/// <remarks>
/// This action allows the specified role to access and use the schema, but does not grant any additional permissions such as SELECT, INSERT, UPDATE, or DELETE on the objects within the schema.
/// </remarks>
public sealed record GrantSchemaUsage(string SchemaName, string Role) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
