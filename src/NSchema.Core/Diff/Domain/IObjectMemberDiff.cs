namespace NSchema.Diff.Domain;

/// <summary>
/// A change to one of a schema object's members: a column, a key, a constraint, an index or a trigger.
/// </summary>
/// <remarks>
/// A member diff records its own name but not its owner, so it does not address itself; the owning object's
/// address supplies the rest (<c>tableDiff.Address.Member(memberDiff.Name)</c>).
/// </remarks>
public interface IObjectMemberDiff : IDatabaseElementDiff;
