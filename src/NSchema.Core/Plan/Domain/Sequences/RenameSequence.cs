using NSchema.Model;

namespace NSchema.Plan.Domain.Sequences;

/// <summary>
/// Represents renaming an existing sequence.
/// </summary>
/// <param name="Sequence">The address of the sequence.</param>
/// <param name="NewName">The new name of the sequence.</param>
public sealed record RenameSequence(ObjectAddress Sequence, SqlIdentifier NewName) : MigrationAction;
