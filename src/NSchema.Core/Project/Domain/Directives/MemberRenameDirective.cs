using NSchema.Model;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// A table-member rename directive.
/// </summary>
/// <remarks>Renames never move a member across objects, so the target is a bare name.</remarks>
/// <param name="From">The member's current address.</param>
/// <param name="To">The declared name the member is renamed to.</param>
public sealed record MemberRenameDirective(MemberAddress From, SqlIdentifier To)
{
    /// <summary>
    /// The member's address after the rename — the same owner, the new name.
    /// </summary>
    public MemberAddress ToAddress => From with { Member = To };
}
