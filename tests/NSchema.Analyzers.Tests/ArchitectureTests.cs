namespace NSchema.Analyzers.Tests;

/// <summary>
/// The layering table's own integrity. The analyzers prove the code obeys the table; nothing they can see proves
/// the table is worth obeying, so that is checked here.
/// </summary>
public sealed class ArchitectureTests
{
    public static TheoryData<string> Slices => [.. Architecture.Dependencies.Keys];

    /// <summary>
    /// A table that let two slices list each other would permit a cycle by decree, and every reference between them
    /// would pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(Slices))]
    public void Layering_IsAcyclic(string slice)
    {
        // Arrange
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(Architecture.AllowedFor(slice));

        // Act
        while (pending.TryDequeue(out var next))
        {
            if (reachable.Add(next))
            {
                foreach (var onward in Architecture.AllowedFor(next))
                {
                    pending.Enqueue(onward);
                }
            }
        }

        // Assert
        reachable.ShouldNotContain(slice, $"'{slice}' can reach itself through the declared layering");
    }

    /// <summary>
    /// A row naming a slice that does not exist is an edge to nowhere, which no rule would ever report on.
    /// </summary>
    [Theory]
    [MemberData(nameof(Slices))]
    public void Layering_NamesOnlyDeclaredSlices(string slice)
    {
        // Act
        var unknown = Architecture.Dependencies[slice].Except(Architecture.Slices);

        // Assert
        unknown.ShouldBeEmpty($"'{slice}' declares a dependency on something that is not a slice");
    }

    [Fact]
    public void Kernel_IsDeclaredInTheLayering()
    {
        // Act
        var missing = Architecture.Kernel.Except(Architecture.Dependencies.Keys);

        // Assert
        missing.ShouldBeEmpty("a kernel slice still needs a row saying what it may use");
    }

    /// <summary>
    /// The table keeps up with the code. NS0004 catches a slice the table has not heard of; this catches the other
    /// direction — a row left behind after its folder was renamed away, which governs nothing while looking as
    /// though it governs a slice.
    /// </summary>
    [Theory]
    [MemberData(nameof(Slices))]
    public void DeclaredSlice_ExistsInTheEngine(string slice)
    {
        // Act
        var namespaces = typeof(NSchemaApplication).Assembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(space => space is not null && space.StartsWith("NSchema.", StringComparison.Ordinal))
            .Select(space => space!.Split('.')[1]);

        // Assert
        namespaces.ShouldContain(slice, $"'{slice}' has a row in the layering table but no code");
    }
}
