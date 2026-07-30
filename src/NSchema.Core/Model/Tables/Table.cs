using System.Diagnostics;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Triggers;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a database table. Adopts its members.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public sealed class Table : SchemaObject, IEquatable<Table>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.Table;

    /// <summary>
    /// The primary key of the table.
    /// </summary>
    public PrimaryKey? PrimaryKey
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }
            value?.Parent = this;
            field?.Parent = null;
            field = value;
        }
    }

    /// <summary>
    /// A list of columns that are part of the table.
    /// </summary>
    public ObjectMemberCollection<Column> Columns
    {
        get => field ??= new ObjectMemberCollection<Column>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public ObjectMemberCollection<ForeignKey> ForeignKeys
    {
        get => field ??= new ObjectMemberCollection<ForeignKey>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of unique constraints defined on the table.
    /// </summary>
    public ObjectMemberCollection<UniqueConstraint> UniqueConstraints
    {
        get => field ??= new ObjectMemberCollection<UniqueConstraint>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of check constraints defined on the table.
    /// </summary>
    public ObjectMemberCollection<CheckConstraint> CheckConstraints
    {
        get => field ??= new ObjectMemberCollection<CheckConstraint>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of exclusion constraints defined on the table.
    /// </summary>
    public ObjectMemberCollection<ExclusionConstraint> ExclusionConstraints
    {
        get => field ??= new ObjectMemberCollection<ExclusionConstraint>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of indexes that are defined on the table.
    /// </summary>
    public ObjectMemberCollection<TableIndex> Indexes
    {
        get => field ??= new ObjectMemberCollection<TableIndex>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of grants that define the permissions associated with the table.
    /// </summary>
    public List<TableGrant> Grants { get; init; } = [];

    /// <summary>
    /// A list of triggers defined on the table.
    /// </summary>
    public ObjectMemberCollection<Trigger> Triggers
    {
        get => field ??= new ObjectMemberCollection<Trigger>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// Merges a table fragment when none of its members conflict with this table.
    /// </summary>
    public Result<int> TryMergeMembers(
        Table members,
        int columnPosition,
        SqlIdentifier? replaceReferenceSchema = null,
        SqlIdentifier? replacementReferenceSchema = null
    )
    {
        var conflicts = MemberConflicts(members);
        if (conflicts.Count > 0)
        {
            return Result.Failure<int>(conflicts);
        }

        foreach (var column in members.Columns)
        {
            Columns.Insert(columnPosition++, column.Clone());
        }

        if (members.PrimaryKey is not null)
        {
            PrimaryKey = members.PrimaryKey.Clone();
        }

        foreach (var foreignKey in members.ForeignKeys)
        {
            var copy = foreignKey.Clone();
            if (copy.References.Schema == replaceReferenceSchema && replacementReferenceSchema is not null)
            {
                copy.References = copy.References with { Schema = replacementReferenceSchema };
            }
            ForeignKeys.Add(copy);
        }

        AddClones(members.UniqueConstraints, UniqueConstraints);
        AddClones(members.CheckConstraints, CheckConstraints);
        AddClones(members.ExclusionConstraints, ExclusionConstraints);
        AddClones(members.Indexes, Indexes);
        AddClones(members.Triggers, Triggers);

        return Result.Success(members.Columns.Count);
    }

    private List<Diagnostic> MemberConflicts(Table members)
    {
        var conflicts = new List<Diagnostic>();
        conflicts.AddRange(Conflicts(Columns, members.Columns));
        if (PrimaryKey is not null && members.PrimaryKey is not null)
        {
            conflicts.Add(TableDiagnostics.DuplicatePrimaryKey(Name));
        }
        conflicts.AddRange(Conflicts(ForeignKeys, members.ForeignKeys));
        conflicts.AddRange(Conflicts(UniqueConstraints, members.UniqueConstraints));
        conflicts.AddRange(Conflicts(CheckConstraints, members.CheckConstraints));
        conflicts.AddRange(Conflicts(ExclusionConstraints, members.ExclusionConstraints));
        conflicts.AddRange(Conflicts(Indexes, members.Indexes));
        conflicts.AddRange(Conflicts(Triggers, members.Triggers));
        return conflicts;
    }

    private IEnumerable<Diagnostic> Conflicts<T>(
        IEnumerable<T> existing,
        IEnumerable<T> incoming) where T : ObjectMember =>
        incoming.Where(candidate => existing.Any(member => member.Name == candidate.Name))
            .Select(candidate => TableDiagnostics.DuplicateMember(Name, candidate.Kind, candidate.Name));

    private static void AddClones<T>(IEnumerable<T> source, ICollection<T> destination) where T : ObjectMember
    {
        foreach (var member in source)
        {
            destination.Add((T)member.Clone());
        }
    }

    /// <inheritdoc/>
    public override Table Clone() => new()
    {
        Name = Name,
        PrimaryKey = PrimaryKey?.Clone(),
        Columns = [.. Columns.Select(c => c.Clone())],
        ForeignKeys = [.. ForeignKeys.Select(k => k.Clone())],
        UniqueConstraints = [.. UniqueConstraints.Select(u => u.Clone())],
        CheckConstraints = [.. CheckConstraints.Select(c => c.Clone())],
        ExclusionConstraints = [.. ExclusionConstraints.Select(x => x.Clone())],
        Indexes = [.. Indexes.Select(i => i.Clone())],
        Grants = [.. Grants],
        Triggers = [.. Triggers.Select(t => t.Clone())],
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(Table? other) =>
        other is not null
        && Name == other.Name
        && Equals(PrimaryKey, other.PrimaryKey)
        && Columns.SequenceEqual(other.Columns)
        && ForeignKeys.SequenceEqual(other.ForeignKeys)
        && UniqueConstraints.SequenceEqual(other.UniqueConstraints)
        && CheckConstraints.SequenceEqual(other.CheckConstraints)
        && ExclusionConstraints.SequenceEqual(other.ExclusionConstraints)
        && Indexes.SequenceEqual(other.Indexes)
        && Grants.SequenceEqual(other.Grants)
        && Triggers.SequenceEqual(other.Triggers);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Table other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Columns.Count);
}
