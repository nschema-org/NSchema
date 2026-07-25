using System.Text.Json;
using NSchema.Configuration.Domain;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The semantic-version value object: SemVer 2.0 precedence with NuGet's case-insensitive prerelease
/// comparison, and equality that agrees with precedence.
/// </summary>
public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("5.0.1", "5.0.1")]
    [InlineData("5.0", "5.0.0")]
    [InlineData("5.0.0.1", "5.0.0.1")]
    [InlineData("5.0.0-alpha.1", "5.0.0-alpha.1")]
    [InlineData("5.0.0+build.7", "5.0.0")]
    public void Parse_RendersCanonically(string text, string canonical)
        => SemanticVersion.Parse(text).ToString().ShouldBe(canonical);

    [Fact]
    public void Parse_InvalidText_Throws()
        => Should.Throw<FormatException>(() => SemanticVersion.Parse("banana"))
            .Message.ShouldContain("not a valid semantic version");

    [Theory]
    [InlineData("5.0.0-alpha.1", "5.0.0")] // a prerelease precedes its release
    [InlineData("5.0.0-alpha", "5.0.0-alpha.1")] // fewer identifiers precede more
    [InlineData("5.0.0-alpha.2", "5.0.0-alpha.10")] // numeric identifiers compare numerically
    [InlineData("5.0.0-1", "5.0.0-alpha")] // numeric identifiers precede alphanumeric ones
    [InlineData("5.0.9", "5.1.0")]
    public void CompareTo_FollowsSemVerPrecedence(string lower, string higher)
        => SemanticVersion.Parse(lower).CompareTo(SemanticVersion.Parse(higher)).ShouldBeLessThan(0);

    [Fact]
    public void Equality_AgreesWithPrecedence()
    {
        // Case and numeric leading zeros are insignificant to precedence, so also to equality and hashing.
        SemanticVersion.Parse("5.0.0-ALPHA.01").ShouldBe(SemanticVersion.Parse("5.0.0-alpha.1"));
        SemanticVersion.Parse("5.0.0-ALPHA.01").GetHashCode().ShouldBe(SemanticVersion.Parse("5.0.0-alpha.1").GetHashCode());
        SemanticVersion.Parse("5.0.0+build.7").ShouldBe(SemanticVersion.Parse("5.0.0")); // metadata never affects precedence
        SemanticVersion.Parse("5.0.0-alpha").ShouldNotBe(SemanticVersion.Parse("5.0.0"));
    }

    [Fact]
    public void Json_RoundTripsAsCanonicalText()
    {
        // Arrange
        var version = SemanticVersion.Parse("5.0.0-alpha.1");

        // Act
        var json = JsonSerializer.Serialize(version);

        // Assert
        json.ShouldBe("\"5.0.0-alpha.1\"");
        JsonSerializer.Deserialize<SemanticVersion>(json).ShouldBe(version);
    }
}
