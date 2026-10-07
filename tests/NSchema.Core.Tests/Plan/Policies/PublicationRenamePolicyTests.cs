using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Publications;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public sealed class PublicationRenamePolicyTests
{
    private readonly PublicationRenamePolicy _sut = new();

    [Fact]
    public void RenamedPublication_Warns()
    {
        // Act
        var diagnostic = _sut.Validate(new DatabaseDiff([]) { Publications = [PublicationDiff.Modified("feed") with { RenamedFrom = "legacy_feed" }] })
            .ShouldHaveSingleItem();

        // Assert
        diagnostic.Code.ShouldBe("publication-renamed");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void UnrenamedPublication_IsQuiet()
        => _sut.Validate(new DatabaseDiff([]) { Publications = [PublicationDiff.Removed("feed")] }).ShouldBeEmpty();
}
