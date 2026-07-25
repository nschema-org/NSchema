using System.Diagnostics;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Model.Schemas;

/// <summary>
/// Represents the definition of a database schema. Adopts the objects it is given.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public sealed class Schema : DatabaseElement, IEquatable<Schema>
{
    /// <inheritdoc/>
    public override SchemaAddress Address => new(Name);

    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<Table> Tables
    {
        get => field ??= new DatabaseObjectCollection<Table>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<View> Views
    {
        get => field ??= new DatabaseObjectCollection<View>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of enum types that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<EnumType> Enums
    {
        get => field ??= new DatabaseObjectCollection<EnumType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<Sequence> Sequences
    {
        get => field ??= new DatabaseObjectCollection<Sequence>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of routines (functions and procedures) that are part of the schema. Functions and procedures share
    /// one name space, so they live in a single list.
    /// </summary>
    public DatabaseObjectCollection<Routine> Routines
    {
        get => field ??= new DatabaseObjectCollection<Routine>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of domains that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<DomainType> Domains
    {
        get => field ??= new DatabaseObjectCollection<DomainType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of composite types that are part of the schema.
    /// </summary>
    public DatabaseObjectCollection<CompositeType> CompositeTypes
    {
        get => field ??= new DatabaseObjectCollection<CompositeType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public List<SchemaGrant> Grants { get; init; } = [];

    /// <summary>
    /// Every schema-level object the schema contains, in one sequence.
    /// </summary>
    public IEnumerable<DatabaseObject> Objects() =>
        Tables.Cast<DatabaseObject>()
        .Concat(Views)
        .Concat(Enums)
        .Concat(Sequences)
        .Concat(Routines)
        .Concat(Domains)
        .Concat(CompositeTypes);

    /// <inheritdoc/>
    public override Schema Clone() => new()
    {
        Name = Name,
        Tables = [.. Tables.Select(t => t.Clone())],
        Grants = [.. Grants],
        Views = [.. Views.Select(v => v.Clone())],
        Enums = [.. Enums.Select(e => e.Clone())],
        Sequences = [.. Sequences.Select(s => s.Clone())],
        Routines = [.. Routines.Select(r => r.Clone())],
        Domains = [.. Domains.Select(d => d.Clone())],
        CompositeTypes = [.. CompositeTypes.Select(t => t.Clone())],
        Comment = Comment,
    };

    /// <summary>
    /// Returns a copy of the schema restricted to the objects whose identity is in the set. Grants ride the
    /// schema and table members ride their table.
    /// </summary>
    public Schema FilteredTo(IdentitySet identities)
    {
        var copy = Clone();
        copy.Tables.RemoveWhere(t => !identities.Contains(t));
        copy.Views.RemoveWhere(v => !identities.Contains(v));
        copy.Enums.RemoveWhere(e => !identities.Contains(e));
        copy.Sequences.RemoveWhere(s => !identities.Contains(s));
        copy.Routines.RemoveWhere(r => !identities.Contains(r));
        copy.Domains.RemoveWhere(d => !identities.Contains(d));
        copy.CompositeTypes.RemoveWhere(t => !identities.Contains(t));
        return copy;
    }

    /// <summary>
    /// Structural equality over the declared contents; the comment is excluded.
    /// </summary>
    public bool Equals(Schema? other) =>
        other is not null
        && Name == other.Name
        && Grants.SequenceEqual(other.Grants)
        && Tables.SequenceEqual(other.Tables)
        && Views.SequenceEqual(other.Views)
        && Enums.SequenceEqual(other.Enums)
        && Sequences.SequenceEqual(other.Sequences)
        && Routines.SequenceEqual(other.Routines)
        && Domains.SequenceEqual(other.Domains)
        && CompositeTypes.SequenceEqual(other.CompositeTypes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Schema other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Tables.Count, Views.Count);
}
