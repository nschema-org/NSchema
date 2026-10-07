using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents setting, changing, or clearing the comment on a publication.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetPublicationComment(SqlIdentifier PublicationName, string? OldComment, string? NewComment) : MigrationAction;
