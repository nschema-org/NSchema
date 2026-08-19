using NSchema.Model;

namespace NSchema.Plan.Domain.Views;

/// <summary>
/// Represents setting, changing, or clearing the comment on a view.
/// </summary>
/// <param name="View">The address of the view.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
/// <param name="IsMaterialized">Whether the commented view is a materialized view.</param>
public sealed record SetViewComment(ObjectAddress View, string? OldComment, string? NewComment, bool IsMaterialized = false) : MigrationAction;
