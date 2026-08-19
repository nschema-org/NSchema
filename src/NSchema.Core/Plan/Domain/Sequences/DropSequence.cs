using NSchema.Model;

namespace NSchema.Plan.Domain.Sequences;

/// <summary>
/// Represents the removal of an existing sequence from the database schema.
/// </summary>
/// <param name="Sequence">The address of the sequence.</param>
public sealed record DropSequence(ObjectAddress Sequence) : MigrationAction;
