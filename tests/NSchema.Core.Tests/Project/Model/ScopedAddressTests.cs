using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The script-address contract: the container is genuinely optional (null = global), with component-wise
/// identifier equality.
/// </summary>
public class ScopedAddressTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreDifferentAddresses()
    {
        // Arrange
        var lower = new ScopedAddress("sales", "seed");
        var mixed = new ScopedAddress("Sales", "SEED");

        // Assert
        lower.ShouldNotBe(mixed);
        lower.ShouldBe(new ScopedAddress("sales", "seed"));
    }

    [Fact]
    public void Equals_SameNameInDifferentScopes_AreDistinctScripts()
    {
        // Arrange
        var sales = new ScopedAddress("sales", "seed");
        var billing = new ScopedAddress("billing", "seed");
        var global = new ScopedAddress(null, "seed");

        // Assert
        sales.ShouldNotBe(billing);
        sales.ShouldNotBe(global);
    }

    [Fact]
    public void ToString_Scoped_RendersLikeAnyOtherReference()
        => new ScopedAddress("Sales", "seed").ToString().ShouldBe("Sales.seed");

    [Fact]
    public void ToString_Global_RendersTheBareName()
        => new ScopedAddress(null, "seed").ToString().ShouldBe("seed");
}
