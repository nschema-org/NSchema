using NSchema.Model;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents setting, changing, or clearing the comment on a composite type.
/// </summary>
/// <param name="Type">The address of the composite type.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetCompositeTypeComment(ObjectAddress Type, string? OldComment, string? NewComment) : MigrationAction;
