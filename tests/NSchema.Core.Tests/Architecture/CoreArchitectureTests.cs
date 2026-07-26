using ArchUnitNET.Domain;

namespace NSchema.Tests.Architecture;

/// <summary>
/// The fixture's own integrity. A rule written against a part that matches nothing passes without testing anything,
/// so the parts are checked to be real before the rules built on them are trusted.
/// </summary>
public sealed class CoreArchitectureTests
{
    public static TheoryData<string, IObjectProvider<IType>> Parts => new()
    {
        { nameof(CoreArchitecture.DomainLayers), CoreArchitecture.DomainLayers },
        { nameof(CoreArchitecture.ApplicationServices), CoreArchitecture.ApplicationServices },
        { nameof(CoreArchitecture.ProviderSeams), CoreArchitecture.ProviderSeams },
    };

    [Theory]
    [MemberData(nameof(Parts))]
    public void NamedPart_MatchesTypes(string name, IObjectProvider<IType> part)
    {
        // Act
        var matched = part.GetObjects(CoreArchitecture.Assembly);

        // Assert
        matched.ShouldNotBeEmpty($"'{name}' matches no types, so every rule written against it passes vacuously");
    }

    [Theory]
    [MemberData(nameof(SliceDependencyTests.Layering), MemberType = typeof(SliceDependencyTests))]
    public void DeclaredPart_MatchesTypes(string part, string[] mayDependOn)
    {
        // Arrange
        _ = mayDependOn;

        // Act
        var matched = CoreArchitecture.In(part).GetObjects(CoreArchitecture.Assembly);

        // Assert
        matched.ShouldNotBeEmpty($"'{part}' matches no types, so its layering rule passes vacuously");
    }

    /// <summary>
    /// The vocabulary keeps up with the code. A new top-level namespace that nobody adds to <see cref="CoreArchitecture"/>
    /// would sit outside every rule, so the parts are checked against what the assembly actually contains.
    /// </summary>
    [Fact]
    public void Parts_CoverEveryTopLevelNamespace()
    {
        // Arrange
        var declared = CoreArchitecture.Parts.Except([CoreArchitecture.Root]);

        // Act
        var actual = typeof(NSchemaApplication).Assembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(space => space is not null && space.StartsWith("NSchema.", StringComparison.Ordinal))
            .Select(space => space!.Split('.')[1])
            .Distinct();

        // Assert
        actual.Except(declared).ShouldBeEmpty("every top-level namespace needs declaring in CoreArchitecture");
    }
}
