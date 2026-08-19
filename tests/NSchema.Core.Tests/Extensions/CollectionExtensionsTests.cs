using NSchema.Extensions;

namespace NSchema.Tests.Extensions;

public sealed class CollectionExtensionsTests
{
    private sealed record Node(string Name, params string[] Deps);

    private static List<string> Order(params Node[] nodes)
    {
        var position = nodes.Select((n, i) => (n.Name, i)).ToDictionary(x => x.Name, x => x.i);
        var edges = nodes.SelectMany((n, i) => n.Deps
                .Where(position.ContainsKey)
                .Select(dep => new DependencyEdge(i, position[dep], Strength: 1)))
            .ToList();
        return [.. nodes.OrderedByDependencies(_ => 0L, edges).Select(n => n.Name)];
    }

    [Fact]
    public void OrderedByDependencies_PutsDependenciesFirst()
    {
        // b depends on a, c depends on b -> a, b, c regardless of input order.
        Order(new Node("c", "b"), new Node("b", "a"), new Node("a")).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void OrderedByDependencies_IgnoresDependenciesOutsideTheSet()
    {
        // a depends on "external" which isn't in the set -> no edge, original order kept.
        Order(new Node("a", "external"), new Node("b")).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void OrderedByDependencies_IsStableForIndependentItems()
    {
        Order(new Node("x"), new Node("y"), new Node("z")).ShouldBe(["x", "y", "z"]);
    }

    [Fact]
    public void OrderedByDependencies_DiamondDependency()
    {
        // d depends on b and c; b and c depend on a -> a before b,c before d.
        var ordered = Order(new Node("d", "b", "c"), new Node("b", "a"), new Node("c", "a"), new Node("a"));
        ordered.ShouldContain("a");
        ordered.IndexOf("a").ShouldBeLessThan(ordered.IndexOf("b"));
        ordered.IndexOf("a").ShouldBeLessThan(ordered.IndexOf("c"));
        ordered.IndexOf("b").ShouldBeLessThan(ordered.IndexOf("d"));
        ordered.IndexOf("c").ShouldBeLessThan(ordered.IndexOf("d"));
    }

    [Fact]
    public void OrderedByDependencies_BreaksAnEqualStrengthCycle_ByInputOrder()
    {
        // a<->b at equal strength: the first-declared item is released first, deterministically.
        Order(new Node("a", "b"), new Node("b", "a")).ShouldBe(["a", "b"]);
    }

    // ── Priority-respecting form ──────────────────────────────────────────────

    [Fact]
    public void OrderedByDependencies_WithPriority_NoEdges_IsExactlyThePriorityOrder()
    {
        // Ties broken by input position, matching a stable sort.
        var items = new[] { "b2", "a1", "c1", "d2" };
        var ordered = items.OrderedByDependencies(item => item[1] - '0', []);
        ordered.ShouldBe(["a1", "c1", "b2", "d2"]);
    }

    [Fact]
    public void OrderedByDependencies_WithPriority_AnEdgeOnlyMovesAnItemAsFarAsItMust()
    {
        // "late" is priority 0 but depends on "early" (priority 1): it runs right after it, and the
        // unconstrained "middle" keeps its place.
        var items = new[] { "late", "middle", "early" };
        var ordered = items.OrderedByDependencies(
            item => item == "late" ? 0L : 1L,
            [new DependencyEdge(Dependent: 0, Dependency: 2, Strength: 1)]);
        ordered.ShouldBe(["middle", "early", "late"]);
    }

    [Fact]
    public void OrderedByDependencies_WithPriority_BreaksCyclesAtTheWeakestEdge_Deterministically()
    {
        // a<->b: the strong edge (a after b) survives, the weak edge (b after a) is cut — b first.
        var items = new[] { "a", "b" };
        var ordered = items.OrderedByDependencies(
            _ => 0L,
            [
                new DependencyEdge(Dependent: 0, Dependency: 1, Strength: 1),
                new DependencyEdge(Dependent: 1, Dependency: 0, Strength: 0),
            ]);
        ordered.ShouldBe(["b", "a"]);
    }

    [Fact]
    public void OrderedByDependencies_WithPriority_SelfAndOutOfRangeEdges_AreIgnored()
    {
        var items = new[] { "a", "b" };
        var ordered = items.OrderedByDependencies(
            _ => 0L,
            [new DependencyEdge(0, 0, 1), new DependencyEdge(0, 99, 1), new DependencyEdge(-1, 1, 1)]);
        ordered.ShouldBe(["a", "b"]);
    }
}
