using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model;
using NSchema.Plan.Domain;

namespace NSchema.Tests.Plan;

public sealed class MigrationPlanTests
{
    private static MigrationPlan Plan(IReadOnlyList<SqlStatement> statements, IdentitySet? adopted = null) =>
        new(new DatabaseDiff([]), statements, Adopted: adopted);

    [Fact]
    public void IsEmpty_NothingDiffersAndNothingIsTakenOver_IsTrue()
        => Plan([]).IsEmpty.ShouldBeTrue();

    [Fact]
    public void IsEmpty_WithADifference_IsFalse()
        => new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]), []).IsEmpty.ShouldBeFalse();

    [Fact]
    public void IsEmpty_AdoptionWithoutSql_IsFalse()
        // Applying it takes objects over, so anything gating on "no changes" must not read it as a no-op.
        => Plan([], new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")])).ShouldSatisfyAllConditions(
            plan => plan.IsEmpty.ShouldBeFalse(),
            plan => plan.HasStatements.ShouldBeFalse());

    [Fact]
    public void HasStatements_WithSql_IsTrue()
        => Plan([new SqlStatement("CREATE SCHEMA app")]).HasStatements.ShouldBeTrue();
}
