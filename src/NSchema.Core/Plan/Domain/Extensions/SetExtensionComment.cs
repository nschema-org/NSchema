using NSchema.Model;

namespace NSchema.Plan.Domain.Extensions;

/// <summary>
/// Represents setting, changing, or clearing the comment on a database extension.
/// </summary>
/// <param name="ExtensionName">The name of the extension.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetExtensionComment(SqlIdentifier ExtensionName, string? OldComment, string? NewComment) : MigrationAction;
