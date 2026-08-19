namespace NSchema.Analyzers.Tests;

/// <summary>
/// The rule that keeps the layering table abreast of the code. Without it a new top-level namespace would simply
/// not be mentioned by any other rule.
/// </summary>
public sealed class UndeclaredSliceAnalyzerTests
{
    private readonly UndeclaredSliceAnalyzer _sut = new();

    [Theory]
    [InlineData("NSchema")]
    [InlineData("NSchema.Plan")]
    [InlineData("NSchema.Plan.Domain.Services")]
    public async Task Namespace_UnderADeclaredSlice_IsAllowed(string declared)
    {
        // Arrange
        var source = $$"""
            namespace {{declared}}
            {
                public sealed class Thing { }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Namespace_UnderAnUndeclaredSlice_IsReported()
    {
        // Arrange
        const string source = """
            namespace NSchema.Mystery.Domain
            {
                public sealed class Thing { }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0004"]);
        diagnostics[0].GetMessage().ShouldBe(
            "Namespace 'NSchema.Mystery.Domain' introduces the slice 'Mystery', "
            + "which is not declared in the layering table");
    }

    [Fact]
    public async Task Namespace_DeclaredFileScoped_IsReadTheSameWay()
    {
        // Arrange
        const string source = """
            namespace NSchema.Mystery;

            public sealed class Thing { }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0004"]);
    }

    [Fact]
    public async Task Namespace_OutsideTheEngine_IsIgnored()
    {
        // Arrange
        const string source = """
            namespace Contoso.Widgets
            {
                public sealed class Thing { }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }
}
