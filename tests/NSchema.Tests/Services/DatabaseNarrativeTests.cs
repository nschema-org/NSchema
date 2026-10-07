using NSchema.Model;
using NSchema.Model.Extensions;
using NSchema.Model.Publications;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class DatabaseNarrativeTests
{
    [Fact]
    public void Counts_EmptyDatabase_HasNoObjects()
        => DatabaseNarrative.Counts(new Database()).ShouldBe("no objects");

    [Fact]
    public void Counts_IncludePublications()
        => DatabaseNarrative.Counts(new Database
        {
            Extensions = [new Extension { Name = "citext" }],
            Publications = [new Publication { Name = "feed" }, new Publication { Name = "audit" }],
        }).ShouldBe("1 extension, 2 publications");
}
