using NSchema.Model;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents the modification of a comment on an existing column in the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldComment">The current comment on the column before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the column after modification. This can be null if the comment is being removed.</param>
public sealed record SetColumnComment(MemberAddress Column, string? OldComment, string? NewComment) : MigrationAction;
