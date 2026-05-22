namespace NSchema.Schema;

/// <summary>
/// Represents a usage grant to a specific role within the database schema.
/// </summary>
/// <param name="Role"></param>
public record SchemaGrant(string Role);
