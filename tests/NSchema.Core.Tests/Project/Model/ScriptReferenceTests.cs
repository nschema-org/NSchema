using NSchema.Model.Scripts;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The script-reference contract: the scope is genuinely optional (null = database-wide), with
/// component-wise identifier equality.
/// </summary>
public class ScriptReferenceTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreDifferentAddresses()
    {
        // Arrange
        var lower = new ScriptReference("sales", "seed");
        var mixed = new ScriptReference("Sales", "SEED");

        // Assert
        lower.ShouldNotBe(mixed);
        lower.ShouldBe(new ScriptReference("sales", "seed"));
    }

    [Fact]
    public void Equals_SameNameInDifferentScopes_AreDistinctScripts()
    {
        // Arrange
        var sales = new ScriptReference("sales", "seed");
        var billing = new ScriptReference("billing", "seed");
        var global = new ScriptReference(null, "seed");

        // Assert
        sales.ShouldNotBe(billing);
        sales.ShouldNotBe(global);
    }

    [Fact]
    public void ToString_Scoped_RendersLikeAnyOtherReference()
        => new ScriptReference("Sales", "seed").ToString().ShouldBe("Sales.seed");

    [Fact]
    public void ToString_Global_RendersTheBareName()
        => new ScriptReference(null, "seed").ToString().ShouldBe("seed");
}
