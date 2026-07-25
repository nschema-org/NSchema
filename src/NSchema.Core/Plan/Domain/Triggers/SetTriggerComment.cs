using NSchema.Model;

namespace NSchema.Plan.Domain.Triggers;

/// <summary>
/// Represents setting, changing, or clearing the comment on a trigger.
/// </summary>
/// <param name="Trigger">The address of the trigger.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetTriggerComment(MemberAddress Trigger, string? OldComment, string? NewComment) : MigrationAction;
