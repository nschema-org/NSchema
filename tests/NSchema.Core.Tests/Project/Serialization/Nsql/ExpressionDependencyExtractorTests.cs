using NSchema.Model;
using NSchema.Model.Services;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class ExpressionDependencyExtractorTests
{
    [Fact]
    public void Names_CollectsTheNamesAnExpressionReads()
        => ExpressionDependencyExtractor.Names("status <> 'draft' AND \"Total\" > 0")
            .ShouldBe([new SqlIdentifier("status"), new SqlIdentifier("AND"), new SqlIdentifier("Total")]);

    [Fact]
    public void Names_SkipsCallsAndLiterals()
        => ExpressionDependencyExtractor.Names("lower(email) = 'x'").ShouldBe([new SqlIdentifier("email")]);

    [Fact]
    public void Names_KeepsTheColumnOfAQualifiedReference()
        => ExpressionDependencyExtractor.Names("orders.status IS NULL").ShouldContain(new SqlIdentifier("status"));
}
