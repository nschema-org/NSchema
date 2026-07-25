using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Which parts of the engine may know about which. Written against <see cref="CoreArchitecture"/>'s named parts,
/// so a new type is governed by where it lives rather than by whether someone remembered to list it.
/// </summary>
public sealed class SliceDependencyTests
{
    /// <summary>
    /// What each part may know about <em>beyond the kernel</em>, which is ambient. A part absent from another's
    /// list may not be referenced from it, so adding an edge is a deliberate decision rather than an accident.
    /// </summary>
    /// <remarks>
    /// A kernel part lists its dependencies in full, since the kernel is ordered within itself. <c>Operations</c> is
    /// the shell that drives the slices, so it may use all of them and nothing may use it. <c>Plugins</c> is the one
    /// slice that may see the composition root, because registering onto the application builder is what a plugin does.
    /// </remarks>
    public static TheoryData<string, string[]> Layering => new()
    {
        { "Diagnostics",   [] },
        { "Extensions",    [] },
        { "Model",         ["Diagnostics", "Extensions"] },
        { "Project",       [] },
        { "State",         [] },
        { "Deployment",    [] },
        { "Configuration", ["Project"] },
        { "Plugins",       ["Project", "Configuration", CoreArchitecture.Root] },
        { "Diff",          ["Project", "State"] },
        { "Plan",          ["Project", "Diff"] },
        { "Apply",         ["Plan"] },
        { "Operations",    ["Project", "State", "Deployment", "Diff", "Plan", "Apply"] },
    };

    [Theory]
    [MemberData(nameof(Layering))]
    public void Part_DependsOnlyOnItsDeclaredLayers(string part, string[] mayDependOn)
    {
        // Arrange
        var forbidden = CoreArchitecture.Parts.Except(DependenciesOf(part, mayDependOn)).Except([part]).ToArray();
        var rule = Types().That().Are(CoreArchitecture.In(part))
            .Should().NotDependOnAny(CoreArchitecture.In(forbidden));

        // Act
        var violations = CoreArchitecture.Violations(rule);

        // Assert
        violations.ShouldBeEmpty($"'{part}' may depend only on {Describe(DependenciesOf(part, mayDependOn))}");
    }

    /// <summary>
    /// The declared layering is itself a DAG. The per-part rules prove the code obeys the table; this proves the
    /// table is worth obeying, since one that let two parts list each other would permit a cycle by decree.
    /// </summary>
    [Theory]
    [MemberData(nameof(Layering))]
    public void Layering_IsAcyclic(string part, string[] mayDependOn)
    {
        // Arrange
        var reachable = new HashSet<string>();
        var pending = new Queue<string>(DependenciesOf(part, mayDependOn));

        // Act
        while (pending.TryDequeue(out var next))
        {
            if (reachable.Add(next))
            {
                foreach (var onward in DependenciesOf(next, DeclaredFor(next)))
                {
                    pending.Enqueue(onward);
                }
            }
        }

        // Assert
        reachable.ShouldNotContain(part, $"'{part}' can reach itself through the declared layering");
    }

    /// <summary>
    /// Every part is accounted for. Without this, adding a top-level namespace would quietly escape the layering
    /// rules altogether — the table would simply not mention it.
    /// </summary>
    [Fact]
    public void Layering_CoversEveryPart()
    {
        // Arrange
        var declared = Layering.Select(row => row.Data.Item1);

        // Act
        var missing = CoreArchitecture.Parts.Except([CoreArchitecture.Root]).Except(declared);

        // Assert
        missing.ShouldBeEmpty("every part of the engine needs a row in the layering table");
    }

    /// <summary>
    /// What a part may depend on in full: its declared list, plus the kernel for anything outside the kernel.
    /// </summary>
    private static string[] DependenciesOf(string part, string[] declared) =>
        CoreArchitecture.Kernel.Contains(part) ? declared : [.. declared.Union(CoreArchitecture.Kernel)];

    private static string[] DeclaredFor(string part) =>
        [.. Layering.Where(row => row.Data.Item1 == part).SelectMany(row => row.Data.Item2)];

    private static string Describe(string[] parts) =>
        parts.Length == 0 ? "nothing" : string.Join(", ", parts);
}
