using System.Text.Json;
using NSchema.Configuration.Domain;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The version-range value object: semantic versions, interval notation, bare-version-means-exact,
/// structural equality, canonical rendering, and no floating versions.
/// </summary>
public sealed class VersionRangeTests
{
    [Theory]
    [InlineData("5.0.1", "[5.0.1]")]
    [InlineData("5.0", "[5.0.0]")]
    [InlineData("5", "[5.0.0]")]
    [InlineData("5.0.0.1", "[5.0.0.1]")]
    [InlineData("5.0.0-alpha.1", "[5.0.0-alpha.1]")]
    [InlineData("5.0.0+build.7", "[5.0.0]")]
    [InlineData(" 5.0.1 ", "[5.0.1]")]
    public void Parse_BareVersion_MeansExact(string text, string canonical)
        => VersionRange.Parse(text).ToString().ShouldBe(canonical);

    [Theory]
    [InlineData("[5.0,6.0)", "[5.0.0,6.0.0)")]
    [InlineData("[5.0, 6.0]", "[5.0.0,6.0.0]")]
    [InlineData("(5.0,6.0)", "(5.0.0,6.0.0)")]
    [InlineData("[5.0,)", "[5.0.0,)")]
    [InlineData("(,6.0)", "(,6.0.0)")]
    [InlineData("(,6.0]", "(,6.0.0]")]
    [InlineData("[5.0.1]", "[5.0.1]")]
    [InlineData("[5.0.0-alpha,6.0)", "[5.0.0-alpha,6.0.0)")]
    public void Parse_Interval_RendersCanonically(string text, string canonical)
        => VersionRange.Parse(text).ToString().ShouldBe(canonical);

    [Theory]
    [InlineData("5.0.1", true)]
    [InlineData("[5.0.1]", true)]
    [InlineData("[5.0,6.0)", false)]
    [InlineData("[5.0,)", false)]
    [InlineData("(,6.0)", false)]
    public void IsExact_TrueOnlyForASingleVersion(string text, bool expected)
        => VersionRange.Parse(text).IsExact.ShouldBe(expected);

    [Fact]
    public void ExactVersion_ReturnsThePinnedVersion()
        => VersionRange.Parse("5.0.1").ExactVersion.ShouldBe(SemanticVersion.Parse("5.0.1"));

    [Fact]
    public void ExactVersion_IsNullForARange()
        => VersionRange.Parse("[5.0,6.0)").ExactVersion.ShouldBeNull();

    [Fact]
    public void Equality_IsStructural()
    {
        VersionRange.Parse("[5.0,6.0)").ShouldBe(VersionRange.Parse("[5.0.0, 6.0.0)"));
        VersionRange.Parse("5.0.1").ShouldBe(VersionRange.Parse("[5.0.1]"));
        VersionRange.Parse("[5.0,6.0)").ShouldNotBe(VersionRange.Parse("[5.0,6.0]"));
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("")]
    [InlineData("~> 5.0")]
    [InlineData("5.0.*")]
    [InlineData("*")]
    [InlineData("[5.0")]
    [InlineData("5.0,6.0)")]
    [InlineData("[5.0.1)")]
    [InlineData("(5.0.1]")]
    [InlineData("(5.0.1)")]
    [InlineData("[,6.0)")]
    [InlineData("[5.0,]")]
    [InlineData("(,)")]
    [InlineData("[5.0,6.0,7.0)")]
    [InlineData("[banana,6.0)")]
    [InlineData("5.0.0-")]
    [InlineData("5.0.0-alpha..1")]
    [InlineData("1.2.3.4.5")]
    public void TryParse_InvalidText_Fails(string text)
        => VersionRange.TryParse(text, out _).ShouldBeFalse();

    [Fact]
    public void Parse_InvalidText_Throws()
        => Should.Throw<FormatException>(() => VersionRange.Parse("banana"))
            .Message.ShouldContain("not a valid version or version range");

    [Theory]
    [InlineData("[5.0,6.0)", "5.0.0", true)]
    [InlineData("[5.0,6.0)", "5.9.9", true)]
    [InlineData("[5.0,6.0)", "6.0.0", false)]
    [InlineData("[5.0,6.0)", "4.9.9", false)]
    [InlineData("(5.0,6.0]", "5.0.0", false)]
    [InlineData("(5.0,6.0]", "6.0.0", true)]
    [InlineData("[5.0,)", "500.0.0", true)]
    [InlineData("(,6.0)", "0.0.1", true)]
    [InlineData("[5.0.1]", "5.0.1", true)]
    [InlineData("[5.0.1]", "5.0.2", false)]
    [InlineData("5.0.1", "5.0.1", true)]
    [InlineData("5.0.1", "5.0.2", false)]
    public void Satisfies_ChecksTheBounds(string range, string version, bool expected)
        => VersionRange.Parse(range).Satisfies(SemanticVersion.Parse(version)).ShouldBe(expected);

    [Theory]
    [InlineData("[5.0,6.0)", "5.0.0-alpha.1", false)] // a prerelease precedes its release
    [InlineData("[5.0,6.0)", "5.1.0-beta", false)] // inside the interval, but a stable range admits no prereleases
    [InlineData("[5.0.0-alpha,6.0)", "5.0.0-alpha.1", true)]
    [InlineData("[5.0.0-beta,6.0)", "5.0.0-alpha.1", false)]
    [InlineData("[5.0.0-alpha.2,6.0)", "5.0.0-alpha.10", true)] // numeric identifiers compare numerically
    [InlineData("[5.0.0-ALPHA,6.0)", "5.0.0-alpha", true)] // case-insensitive, as NuGet compares them
    [InlineData("[5.0.0-alpha,6.0)", "5.0.0", true)]
    public void Satisfies_OrdersPrereleasesBySemVerPrecedence(string range, string version, bool expected)
        => VersionRange.Parse(range).Satisfies(SemanticVersion.Parse(version)).ShouldBe(expected);

    [Fact]
    public void Highest_PicksTheHighestSatisfyingVersion()
        => VersionRange.Parse("[5.0,6.0)").Highest(
            [SemanticVersion.Parse("5.1.0"), SemanticVersion.Parse("6.1.0"), SemanticVersion.Parse("5.3.2"), SemanticVersion.Parse("5.2.0")])
            .ShouldBe(SemanticVersion.Parse("5.3.2"));

    [Fact]
    public void Highest_NoneSatisfy_ReturnsNull()
        => VersionRange.Parse("[5.0,6.0)").Highest([SemanticVersion.Parse("4.9.0"), SemanticVersion.Parse("6.0.0")]).ShouldBeNull();

    [Fact]
    public void Highest_ExcludesPrereleases_FromAStableRange()
        => VersionRange.Parse("[5.0,6.0)").Highest(
            [SemanticVersion.Parse("5.1.0"), SemanticVersion.Parse("5.2.0-beta"), SemanticVersion.Parse("5.1.5")])
            .ShouldBe(SemanticVersion.Parse("5.1.5"));

    [Fact]
    public void Highest_AdmitsPrereleases_WhenABoundIsPrerelease()
        => VersionRange.Parse("[5.0.0-alpha,6.0)").Highest(
            [SemanticVersion.Parse("5.1.0"), SemanticVersion.Parse("5.2.0-beta")])
            .ShouldBe(SemanticVersion.Parse("5.2.0-beta"));

    [Fact]
    public void Json_RoundTripsAsCanonicalText()
    {
        // Arrange
        var range = VersionRange.Parse("[5.0,6.0)");

        // Act
        var json = JsonSerializer.Serialize(range);

        // Assert
        json.ShouldBe("\"[5.0.0,6.0.0)\"");
        JsonSerializer.Deserialize<VersionRange>(json).ShouldBe(range);
    }
}
