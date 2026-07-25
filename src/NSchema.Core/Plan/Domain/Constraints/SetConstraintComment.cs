using NSchema.Model;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents setting or clearing the comment on an existing constraint.
/// </summary>
/// <param name="Constraint">The address of the constraint.</param>
/// <param name="OldComment">The current comment before modification, or null if there is none.</param>
/// <param name="NewComment">The new comment to set, or null if the comment is being removed.</param>
public sealed record SetConstraintComment(MemberAddress Constraint, string? OldComment, string? NewComment) : MigrationAction;
