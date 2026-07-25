using NSchema.Model;

namespace NSchema.Plan.Domain.Enums;

/// <summary>
/// Represents setting, changing, or clearing the comment on an enum type.
/// </summary>
/// <param name="Enum">The address of the enum.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetEnumComment(ObjectAddress Enum, string? OldComment, string? NewComment) : MigrationAction;
