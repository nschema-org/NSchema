using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The address contract: always fully qualified, with component-wise identifier equality.
/// </summary>
public class ObjectAddressTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreDifferentAddresses()
    {
        // Arrange
        var lower = new ObjectAddress("app", "users");
        var mixed = new ObjectAddress("App", "USERS");

        // Assert
        lower.ShouldNotBe(mixed);
        lower.ShouldBe(new ObjectAddress("app", "users"));
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new ObjectAddress("App", "Users").ToString().ShouldBe("App.Users");
    }
}
