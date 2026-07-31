using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public sealed class AdoptionPolicyTests
{
    private readonly AdoptionPolicy _sut = new();

    private static MigrationPlan Adopting(IdentitySet adopted) =>
        new(new DatabaseDiff([]), []) { Adopted = adopted };

    [Fact]
    public void Validate_NothingAdopted_PassesClean()
        => _sut.Validate(Adopting(IdentitySet.Empty)).ShouldBeEmpty();

    [Fact]
    public void Validate_AdoptedObjects_AreReported()
    {
        // Arrange
        var plan = Adopting(new IdentitySet(
            DatabaseObjects: [DatabaseAddress.Schema("app")],
            SchemaObjects: [ObjectAddress.Table("app", "users")]));

        // Act
        var diagnostic = _sut.Validate(plan).ShouldHaveSingleItem();

        // Assert — an apply changes nothing about these, so nothing else in the plan mentions them.
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Source.ShouldBe("adoption");
        diagnostic.Message.ShouldContain("2 existing objects");
        diagnostic.Message.ShouldContain("app, app.users");
    }

    [Fact]
    public void Validate_ManyAdoptedObjects_ListsTheFirstFew()
    {
        // Arrange
        var plan = Adopting(new IdentitySet(
            SchemaObjects: [.. Enumerable.Range(1, 7).Select(i => ObjectAddress.Table("app", $"t{i}"))]));

        // Act
        var diagnostic = _sut.Validate(plan).ShouldHaveSingleItem();

        // Assert
        diagnostic.Message.ShouldContain("7 existing objects");
        diagnostic.Message.ShouldContain("app.t1, app.t2, app.t3, app.t4, app.t5, and 2 others");
    }
}
