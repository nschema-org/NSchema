using NSchema.Model;

namespace NSchema.Plan.Domain.Indexes;

/// <summary>
/// Represents the modification of a comment on an existing index in the database schema.
/// </summary>
/// <param name="Index">The address of the index.</param>
/// <param name="OldComment">The current comment on the index before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the index after modification. This can be null if the comment is being removed.</param>
public sealed record SetIndexComment(MemberAddress Index, string? OldComment, string? NewComment) : MigrationAction;
