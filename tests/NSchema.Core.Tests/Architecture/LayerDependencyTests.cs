using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace NSchema.Tests.Architecture;

/// <summary>
/// How the layers within a slice relate. Where <see cref="SliceDependencyTests"/> governs which slices may know
/// about each other, these govern the direction inside one: a slice's domain answers questions about the schema,
/// and the services around it orchestrate.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void DomainLayers_DoNotDependOnApplicationServices()
    {
        // Arrange — the contract a slice is called through composes its domain, never the other way about.
        var rule = Types().That().Are(CoreArchitecture.DomainLayers)
            .Should().NotDependOnAny(CoreArchitecture.ApplicationServices);

        // Act
        var violations = CoreArchitecture.Violations(rule);

        // Assert
        violations.ShouldBeEmpty("a slice's domain may not call the services that orchestrate it");
    }

    [Fact]
    public void ProviderSeams_DoNotDependOnOperations()
    {
        // Arrange — a provider implements a seam; how the engine sequences a run is none of its business.
        var rule = Types().That().Are(CoreArchitecture.ProviderSeams)
            .Should().NotDependOnAny(CoreArchitecture.In("Operations"));

        // Act
        var violations = CoreArchitecture.Violations(rule);

        // Assert
        violations.ShouldBeEmpty("a provider seam may not depend on the operations that drive it");
    }
}
