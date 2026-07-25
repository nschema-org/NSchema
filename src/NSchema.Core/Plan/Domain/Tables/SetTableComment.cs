using NSchema.Model;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the modification of a comment on an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="OldComment">The current comment on the table before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the table after modification. This can be null if the comment is being removed.</param>
public sealed record SetTableComment(ObjectAddress Table, string? OldComment, string? NewComment) : MigrationAction;
