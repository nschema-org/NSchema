using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Routines;
using NSchema.Model.Tables;
using NSchema.Model.Types;
using NSchema.Model.Views;

namespace NSchema.Model.Services;

/// <summary>
/// A graph representing what requires what across a database.
/// </summary>
/// <remarks>
/// An edge A->B means "A requires B, to exist". Edges are directional, and each answer different questions:
/// what a node needs before it can be created, and what must go before it can be dropped.
/// </remarks>
internal sealed class DependencyGraph
{
    // The graph keys on kind-free locations, not the model's kinded Address properties: an edge arrives as
    // a reference (a foreign key target, a scanned view body, a type name), and a reference does not know
    // the kind of what it names. A kinded key would never meet it. The kind rides on the node instead.
    private readonly Dictionary<Address, List<DependencyNode>> _byAddress = [];
    private readonly Dictionary<ObjectAddress, List<DependencyNode>> _byOwner = [];
    private readonly Dictionary<DependencyNode, List<Edge>> _requires = [];
    private readonly Dictionary<DependencyNode, List<Edge>> _requiredBy = [];
    private readonly ILookup<SqlIdentifier, ObjectAddress> _typesByName;

    private readonly record struct Edge(DependencyNode Node, DependencyCertainty Certainty);

    /// <summary>
    /// Builds the graph of everything <paramref name="database"/> contains.
    /// </summary>
    public DependencyGraph(Database database)
    {
        var allTables = database.Objects<Table>().ToList();
        var allViews = database.Objects<View>().ToList();
        var allTypes = database.Objects<TypeObject>().ToList();

        _typesByName = allTypes.ToLookup(t => t.Object.Name, t => new ObjectAddress(t.Schema, t.Object.Name));

        // Nodes first: an edge can point at anything, including something declared later.
        foreach (var (schema, table) in allTables)
        {
            Add(new DependencyNode(new ObjectAddress(schema, table.Name), DependencyKind.Table));
            foreach (var foreignKey in table.ForeignKeys)
            {
                Add(ConstraintNode(schema, table.Name, foreignKey));
            }
        }

        foreach (var (schema, view) in allViews)
        {
            Add(new DependencyNode(new ObjectAddress(schema, view.Name), DependencyKind.View));
        }

        foreach (var (schema, routine) in database.Objects<Routine>())
        {
            Add(new DependencyNode(new ObjectAddress(schema, routine.Name), DependencyKind.Routine));
        }

        foreach (var (schema, type) in allTypes)
        {
            Add(new DependencyNode(new ObjectAddress(schema, type.Name), TypeKind(type)));
        }

        foreach (var extension in database.Extensions)
        {
            Add(new DependencyNode(DatabaseAddress.Extension(extension.Name), DependencyKind.Extension));
        }

        // An object an extension provides, requires it.
        foreach (var (schema, provided) in database.Objects<SchemaObject>())
        {
            if (provided.ProvidedBy is not { } provider)
            {
                continue;
            }

            foreach (var node in At(new ObjectAddress(schema, provided.Name)))
            {
                Connect(node, DatabaseAddress.Extension(provider.Name), DependencyCertainty.Stated);
            }
        }

        foreach (var (schema, table) in allTables)
        {
            foreach (var foreignKey in table.ForeignKeys)
            {
                // The constraint requires the table it points at — not the table that owns it, which is
                // containment. So dropping the referenced table costs the constraint, and nothing more.
                // The model names that table outright, so the edge is exact.
                Connect(ConstraintNode(schema, table.Name, foreignKey),
                    foreignKey.References,
                    DependencyCertainty.Stated);
            }
        }

        // A view's dependencies are embedded in its body: there is nothing to sever but the view itself.
        // What it reads is scanned out of SQL nobody parsed, so the edge is a guess — a good one for
        // ordering two things already in a plan, not good enough to drag a third into it unannounced.
        // The models answer for themselves, so both sides of a migration carry the edges, however their
        // models were produced.
        foreach (var (schema, view) in allViews)
        {
            var node = new DependencyNode(new ObjectAddress(schema, view.Name), DependencyKind.View);
            foreach (var dependency in view.Reads(schema))
            {
                Connect(node, dependency, DependencyCertainty.Inferred);
            }
        }

        // A routine's references are the same kind of guess, scanned out of its definition: what it reads,
        // and the routines it calls.
        foreach (var (schema, routine) in database.Objects<Routine>())
        {
            var node = new DependencyNode(new ObjectAddress(schema, routine.Name), DependencyKind.Routine);
            foreach (var dependency in routine.References(schema).Where(d => d != node.Address))
            {
                Connect(node, dependency, DependencyCertainty.Inferred);
            }
        }

        // A declared data type may name a user type: from a column, from a domain's base, or from a composite's
        // field. The dependent of a column edge is the column itself — its table is not required to go, but the
        // column cannot outlive its type.
        foreach (var (schema, table) in allTables)
        {
            foreach (var column in table.Columns)
            {
                ConnectToType(new DependencyNode(new MemberAddress(schema, table.Name, column.Name), DependencyKind.Column), column.Type);
            }
        }

        // A table's own expressions may call routines: a computed column's generation expression, a column
        // default, or a check constraint. The edges are scanned out of opaque SQL, so — like a view's — they
        // are inferred: good enough to order two things already in a plan. A column expression's dependent is
        // the column; a check's rides the table.
        foreach (var (schema, table) in allTables)
        {
            var tableNode = new DependencyNode(new ObjectAddress(schema, table.Name), DependencyKind.Table);
            foreach (var column in table.Columns)
            {
                var columnNode = new DependencyNode(new MemberAddress(schema, table.Name, column.Name), DependencyKind.Column);
                ConnectAll(columnNode, column.References(schema));
            }
            foreach (var check in table.CheckConstraints)
            {
                ConnectAll(tableNode, check.References(schema));
            }
        }

        foreach (var (schema, domain) in database.Objects<DomainType>())
        {
            ConnectToType(new DependencyNode(new ObjectAddress(schema, domain.Name), DependencyKind.Domain), domain.DataType);
        }

        foreach (var (schema, composite) in database.Objects<CompositeType>())
        {
            var node = new DependencyNode(new ObjectAddress(schema, composite.Name), DependencyKind.CompositeType);
            foreach (var field in composite.Fields)
            {
                ConnectToType(node, field.DataType);
            }
        }
    }

    /// <summary>
    /// The nodes <paramref name="node"/> requires to exist.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> DependenciesOf(DependencyNode node) => Nodes(_requires, node);

    /// <summary>
    /// The nodes that require <paramref name="node"/> to exist.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> DependentsOf(DependencyNode node) => Nodes(_requiredBy, node);

    /// <summary>
    /// Everything that transitively requires <paramref name="seeds"/>: what else must go before they can.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> AllDependentsOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requiredBy, node));

    /// <summary>
    /// The part of <see cref="AllDependentsOf"/> reachable without believing anything NSchema guessed.
    /// </summary>
    /// <remarks>
    /// The difference between the two is the part a caller should hedge on rather than assert.
    /// </remarks>
    public IReadOnlyCollection<DependencyNode> StatedDependentsOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requiredBy, node, DependencyCertainty.Stated));

    /// <summary>
    /// Everything <paramref name="seeds"/> transitively require: what must exist before they can.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> AllDependenciesOf(IEnumerable<DependencyNode> seeds) =>
        Close(seeds, node => Nodes(_requires, node));

    /// <summary>
    /// The objects the object at <paramref name="address"/> requires, folding what its members require into it.
    /// </summary>
    /// <remarks>
    /// Severing asks about members — which constraint has to go — but ordering asks about objects: a table is
    /// created after the tables its foreign keys point at, whichever constraint carries the edge.
    /// </remarks>
    public IReadOnlyCollection<ObjectAddress> ObjectDependenciesOf(ObjectAddress address) => Owners(address, _requires);

    /// <summary>
    /// The objects that require the object at <paramref name="address"/>: what must go before it can.
    /// </summary>
    public IReadOnlyCollection<ObjectAddress> ObjectDependentsOf(ObjectAddress address) => Owners(address, _requiredBy);

    /// <summary>
    /// As <see cref="ObjectDependenciesOf"/>, but each object carries the strongest certainty of the edges
    /// that reach it — what an ordering should trust when it must break a cycle.
    /// </summary>
    public IReadOnlyCollection<(ObjectAddress Object, DependencyCertainty Certainty)> ObjectDependencyEdgesOf(ObjectAddress address) =>
        OwnerEdges(address, _requires);

    /// <summary>
    /// As <see cref="ObjectDependentsOf"/>, with the same certainty accounting.
    /// </summary>
    public IReadOnlyCollection<(ObjectAddress Object, DependencyCertainty Certainty)> ObjectDependentEdgesOf(ObjectAddress address) =>
        OwnerEdges(address, _requiredBy);

    /// <summary>
    /// The foreign keys pointing at the table at <paramref name="address"/> — the edges into it that can be cut,
    /// where <see cref="ObjectDependentsOf"/> only says who holds them.
    /// </summary>
    public IReadOnlyCollection<MemberAddress> ForeignKeysInto(ObjectAddress address) =>
        [.. Nodes(_requiredBy, new DependencyNode(address, DependencyKind.Table))
            .Where(node => node.Kind == DependencyKind.ForeignKey)
            .Select(node => node.Address)
            .OfType<MemberAddress>()];

    /// <summary>
    /// The nodes living at <paramref name="address"/>, of any kind.
    /// </summary>
    public IReadOnlyCollection<DependencyNode> At(Address address) =>
        _byAddress.TryGetValue(address, out var nodes) ? nodes : [];

    /// <summary>
    /// Walks <paramref name="along"/> from every seed until nothing new turns up.
    /// </summary>
    /// <remarks>
    /// The seeds are excluded — a caller asks what its closure costs it, not what it already has — and a node
    /// is visited once, so a cycle terminates rather than needing the edges to be acyclic.
    /// </remarks>
    private static List<DependencyNode> Close(IEnumerable<DependencyNode> seeds, Func<DependencyNode, IReadOnlyCollection<DependencyNode>> along)
    {
        var seen = new HashSet<DependencyNode>(seeds);
        var pending = new Queue<DependencyNode>(seen);
        var closure = new List<DependencyNode>();

        while (pending.TryDequeue(out var node))
        {
            foreach (var next in along(node).Where(seen.Add))
            {
                closure.Add(next);
                pending.Enqueue(next);
            }
        }

        return closure;
    }

    private static DependencyNode ConstraintNode(SqlIdentifier schema, SqlIdentifier table, ForeignKey foreignKey) =>
        new(new MemberAddress(schema, table, foreignKey.Name), DependencyKind.ForeignKey);

    private static DependencyKind TypeKind(TypeObject type) => type.Kind switch
    {
        SchemaObjectKind.Enum => DependencyKind.Enum,
        SchemaObjectKind.Domain => DependencyKind.Domain,
        SchemaObjectKind.CompositeType => DependencyKind.CompositeType,
        SchemaObjectKind.NativeType => DependencyKind.NativeType,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type.Kind, "Not a type kind."),
    };

    private void ConnectAll(DependencyNode dependent, IReadOnlyList<ObjectAddress> references)
    {
        foreach (var address in references.Where(a => a != dependent.Address && OwnerOf(dependent.Address) != a))
        {
            Add(dependent);
            Connect(dependent, address, DependencyCertainty.Inferred);
        }
    }

    private void ConnectToType(DependencyNode dependent, SqlType type)
    {
        if (ResolveType(type) is not var (address, certainty) || address == dependent.Address)
        {
            return;
        }

        Add(dependent);
        Connect(dependent, address, certainty);
    }

    private (ObjectAddress Address, DependencyCertainty Certainty)? ResolveType(SqlType type)
    {
        if (type.Schema is { } schema)
        {
            return (new ObjectAddress(schema, type.Name), DependencyCertainty.Stated);
        }

        return _typesByName[type.Name].Take(2).ToList() is [var only]
            ? (only, DependencyCertainty.Inferred)
            : null;
    }

    private static IReadOnlyCollection<DependencyNode> Nodes(
        Dictionary<DependencyNode, List<Edge>> edges, DependencyNode node, DependencyCertainty? only = null) =>
        edges.TryGetValue(node, out var found)
            ? [.. found.Where(e => only is null || e.Certainty == only).Select(e => e.Node)]
            : [];

    /// <summary>
    /// Walks <paramref name="edges"/> from every node the object at <paramref name="address"/> owns, and reads
    /// each one that answers back as the object owning it. The object itself is excluded: an edge between two of
    /// its own members says nothing about where it goes.
    /// </summary>
    private IReadOnlyCollection<ObjectAddress> Owners(ObjectAddress address, Dictionary<DependencyNode, List<Edge>> edges) =>
        [.. OwnerEdges(address, edges).Select(e => e.Object)];

    private IReadOnlyCollection<(ObjectAddress Object, DependencyCertainty Certainty)> OwnerEdges(
        ObjectAddress address, Dictionary<DependencyNode, List<Edge>> edges) =>
        _byOwner.TryGetValue(address, out var owned)
            ? [.. owned.SelectMany(node => edges.TryGetValue(node, out var found) ? found : Enumerable.Empty<Edge>())
                .Select(edge => (Owner: OwnerOf(edge.Node.Address), edge.Certainty))
                .Where(x => x.Owner is { } owner && owner != address)
                .GroupBy(x => x.Owner!)
                .Select(g => (g.Key, g.Min(x => x.Certainty)))]
            : [];

    /// <summary>
    /// The object an address belongs to: a member's owner, or a kind-free reading of the object itself.
    /// </summary>
    private static ObjectAddress? OwnerOf(Address address) => address switch
    {
        MemberAddress member => member.Owner,
        ObjectAddress { Kind: null } o => o,
        ObjectAddress o => new ObjectAddress(o.Schema, o.Name),
        _ => null,
    };

    private void Add(DependencyNode node)
    {
        if (!_byAddress.TryGetValue(node.Address, out var atAddress))
        {
            _byAddress[node.Address] = atAddress = [];
        }
        if (!atAddress.Contains(node))
        {
            atAddress.Add(node);
        }

        if (OwnerOf(node.Address) is not { } owner)
        {
            return;
        }

        if (!_byOwner.TryGetValue(owner, out var owned))
        {
            _byOwner[owner] = owned = [];
        }
        if (!owned.Contains(node))
        {
            owned.Add(node);
        }
    }

    /// <summary>
    /// Records that <paramref name="dependent"/> requires whatever lives at <paramref name="address"/>.
    /// </summary>
    /// <remarks>
    /// A dependency on something outside this database — not managed, or simply not here — produces no edge,
    /// the same way the linearizer's sort ignores what it cannot see.
    /// </remarks>
    private void Connect(DependencyNode dependent, Address address, DependencyCertainty certainty)
    {
        foreach (var dependency in At(address))
        {
            Link(_requires, dependent, new Edge(dependency, certainty));
            Link(_requiredBy, dependency, new Edge(dependent, certainty));
        }
    }

    private static void Link(Dictionary<DependencyNode, List<Edge>> edges, DependencyNode from, Edge to)
    {
        if (!edges.TryGetValue(from, out var found))
        {
            edges[from] = found = [];
        }
        if (!found.Contains(to))
        {
            found.Add(to);
        }
    }
}
