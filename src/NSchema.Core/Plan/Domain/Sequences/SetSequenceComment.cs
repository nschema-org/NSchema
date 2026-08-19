using NSchema.Model;

namespace NSchema.Plan.Domain.Sequences;

/// <summary>
/// Represents setting, changing, or clearing the comment on a sequence.
/// </summary>
/// <param name="Sequence">The address of the sequence.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetSequenceComment(ObjectAddress Sequence, string? OldComment, string? NewComment) : MigrationAction;
