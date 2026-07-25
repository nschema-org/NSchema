using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Tables;

/// <summary>
/// Describes a privilege granted to or revoked from a role.
/// </summary>
/// <param name="Kind"><see cref="ChangeKind.Add"/> for a grant, <see cref="ChangeKind.Remove"/> for a revocation.</param>
/// <param name="Role">The role the privilege applies to.</param>
/// <param name="Privileges">The table privileges involved, or <see langword="null"/> for schema-level usage grants.</param>
public sealed record GrantChange(ChangeKind Kind, SqlIdentifier Role, TablePrivilege? Privileges = null);
