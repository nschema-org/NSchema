using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents setting, changing, or clearing the comment on a domain.
/// </summary>
/// <param name="Domain">The address of the domain.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetDomainComment(ObjectAddress Domain, string? OldComment, string? NewComment) : MigrationAction;
