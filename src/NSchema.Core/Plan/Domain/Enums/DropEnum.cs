using NSchema.Model;

namespace NSchema.Plan.Domain.Enums;

/// <summary>
/// Represents the removal of an existing enum type from the database schema.
/// </summary>
/// <param name="Enum">The address of the enum.</param>
public sealed record DropEnum(ObjectAddress Enum) : MigrationAction;
