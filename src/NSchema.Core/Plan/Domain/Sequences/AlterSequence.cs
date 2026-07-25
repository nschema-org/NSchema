using NSchema.Model;
using NSchema.Model.Sequences;

namespace NSchema.Plan.Domain.Sequences;

/// <summary>
/// Represents altering the options of an existing sequence.
/// </summary>
/// <param name="Sequence">The address of the sequence.</param>
/// <param name="OldOptions">The sequence's options before the alteration.</param>
/// <param name="NewOptions">The sequence's options after the alteration.</param>
public sealed record AlterSequence(
    ObjectAddress Sequence,
    SequenceOptions OldOptions,
    SequenceOptions NewOptions
) : MigrationAction;
