namespace NSchema.Schema;

/// <summary>
/// Represents a usage grant to a specific role within the database schema.
/// </summary>
/// <param name="Role">The name of the role to which the usage grant is assigned.</param>
public record SchemaGrant(string Role);
