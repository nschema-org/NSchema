using System.Diagnostics;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Types;
using NSchema.Model.Views;
using NSchema.Model.XmlSchemaCollections;

namespace NSchema.Model.Schemas;

/// <summary>
/// Represents the definition of a database schema. Adopts the objects it is given.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public sealed class Schema : DatabaseObject, IEquatable<Schema>
{
    /// <inheritdoc/>
    public override DatabaseObjectKind Kind => DatabaseObjectKind.Schema;

    /// <inheritdoc/>
    public override DatabaseAddress Address => DatabaseAddress.Schema(Name);

    /// <summary>
    /// A list of tables that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<Table> Tables
    {
        get => field ??= new SchemaObjectCollection<Table>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of views that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<View> Views
    {
        get => field ??= new SchemaObjectCollection<View>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of enum types that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<EnumType> Enums
    {
        get => field ??= new SchemaObjectCollection<EnumType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of sequences that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<Sequence> Sequences
    {
        get => field ??= new SchemaObjectCollection<Sequence>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of routines (functions and procedures) that are part of the schema. Functions and procedures share
    /// one name space, so they live in a single list.
    /// </summary>
    public SchemaObjectCollection<Routine> Routines
    {
        get => field ??= new SchemaObjectCollection<Routine>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of domains that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<DomainType> Domains
    {
        get => field ??= new SchemaObjectCollection<DomainType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of composite types that are part of the schema.
    /// </summary>
    public SchemaObjectCollection<CompositeType> CompositeTypes
    {
        get => field ??= new SchemaObjectCollection<CompositeType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of captured native types that live in the schema.
    /// </summary>
    public SchemaObjectCollection<NativeType> NativeTypes
    {
        get => field ??= new SchemaObjectCollection<NativeType>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// The XML schema collections the schema declares.
    /// </summary>
    public SchemaObjectCollection<XmlSchemaCollection> XmlSchemaCollections
    {
        get => field ??= new SchemaObjectCollection<XmlSchemaCollection>(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of grants that define the permissions associated with the schema.
    /// </summary>
    public List<SchemaGrant> Grants { get; init; } = [];

    /// <summary>
    /// Every schema-level object the schema contains, in one sequence.
    /// </summary>
    public IEnumerable<SchemaObject> Objects() =>
        Tables.Cast<SchemaObject>()
        .Concat(Views)
        .Concat(Enums)
        .Concat(Sequences)
        .Concat(Routines)
        .Concat(Domains)
        .Concat(CompositeTypes)
        .Concat(NativeTypes)
        .Concat(XmlSchemaCollections);

    /// <inheritdoc/>
    public override Schema Clone() => Copy(IsImplicit);

    /// <summary>
    /// A copy held on to for its contents alone.
    /// </summary>
    internal Schema AsImplicit() => Copy(isImplicit: true);

    private Schema Copy(bool isImplicit) => new()
    {
        Name = Name,
        IsImplicit = isImplicit,
        ProvidedBy = ProvidedBy,
        Tables = [.. Tables.Select(t => t.Clone())],
        Grants = [.. Grants],
        Views = [.. Views.Select(v => v.Clone())],
        Enums = [.. Enums.Select(e => e.Clone())],
        Sequences = [.. Sequences.Select(s => s.Clone())],
        Routines = [.. Routines.Select(r => r.Clone())],
        Domains = [.. Domains.Select(d => d.Clone())],
        CompositeTypes = [.. CompositeTypes.Select(t => t.Clone())],
        NativeTypes = [.. NativeTypes.Select(t => t.Clone())],
        XmlSchemaCollections = [.. XmlSchemaCollections.Select(x => x.Clone())],
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
        copy.NativeTypes.RemoveWhere(t => !identities.Contains(t));
        copy.XmlSchemaCollections.RemoveWhere(x => !identities.Contains(x));
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
        && CompositeTypes.SequenceEqual(other.CompositeTypes)
        && NativeTypes.SequenceEqual(other.NativeTypes)
        && XmlSchemaCollections.SequenceEqual(other.XmlSchemaCollections);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Schema other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Tables.Count, Views.Count);
}
