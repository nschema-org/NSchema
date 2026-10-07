using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents renaming a publication.
/// </summary>
/// <param name="OldName">The publication's current name.</param>
/// <param name="NewName">The publication's new name.</param>
public sealed record RenamePublication(SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction;
