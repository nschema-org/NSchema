using NSchema.Model;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents renaming an existing composite type.
/// </summary>
/// <param name="Type">The address of the composite type.</param>
/// <param name="NewName">The new name of the composite type.</param>
public sealed record RenameCompositeType(ObjectAddress Type, SqlIdentifier NewName) : MigrationAction;
