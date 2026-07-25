using NSchema.Model;

namespace NSchema.Plan.Domain.Sequences;

/// <summary>
/// Represents the creation of a sequence.
/// </summary>
/// <param name="SchemaName">The name of the schema the sequence belongs to.</param>
/// <param name="Sequence">The definition of the sequence to create.</param>
public sealed record CreateSequence(SqlIdentifier SchemaName, NSchema.Model.Sequences.Sequence Sequence) : MigrationAction;
