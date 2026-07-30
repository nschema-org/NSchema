namespace NSchema.Model;

/// <summary>
/// The kind of a member a schema object owns.
/// </summary>
public enum MemberKind
{
    /// <summary>
    /// A column.
    /// </summary>
    Column,

    /// <summary>
    /// A primary key.
    /// </summary>
    PrimaryKey,

    /// <summary>
    /// A foreign key.
    /// </summary>
    ForeignKey,

    /// <summary>
    /// A unique constraint.
    /// </summary>
    UniqueConstraint,

    /// <summary>
    /// A check constraint.
    /// </summary>
    CheckConstraint,

    /// <summary>
    /// An exclusion constraint.
    /// </summary>
    ExclusionConstraint,

    /// <summary>
    /// An index.
    /// </summary>
    Index,

    /// <summary>
    /// A trigger.
    /// </summary>
    Trigger
}

/// <summary>
/// Rendering for <see cref="MemberKind"/>.
/// </summary>
internal static class MemberKindExtensions
{
    /// <summary>
    /// The kind as display prose (e.g. <c>"foreign key"</c>), for diagnostics.
    /// </summary>
    public static string Display(this MemberKind kind) => kind switch
    {
        MemberKind.Column => "column",
        MemberKind.PrimaryKey => "primary key",
        MemberKind.ForeignKey => "foreign key",
        MemberKind.UniqueConstraint => "unique constraint",
        MemberKind.CheckConstraint => "check constraint",
        MemberKind.ExclusionConstraint => "exclusion constraint",
        MemberKind.Index => "index",
        MemberKind.Trigger => "trigger",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
