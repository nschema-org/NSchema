using NSchema.Model;

namespace NSchema.Plan.Domain.Enums;

/// <summary>
/// Represents renaming an existing enum type.
/// </summary>
/// <param name="Enum">The address of the enum.</param>
/// <param name="NewName">The new name of the enum.</param>
public sealed record RenameEnum(ObjectAddress Enum, SqlIdentifier NewName) : MigrationAction;
