using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns domain diffs into actions: a base-type change recreates (drop + create), every
/// other facet (default, not-null, checks) alters in place, and domains are ordered before tables / dropped after.
/// </summary>
public sealed class PlanLinearizerDomainTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(DomainDiff domain) =>
        _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Containing("app") with { Domains = [domain] }]));

    [Fact]
    public void AddedDomain_EmitsCreateDomain()
        => Linearize(DomainDiff.Added("app", new DomainType { Name = "d", DataType = SqlType.Text }))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateDomain>().DomainType.Name.ShouldBe("d");

    [Fact]
    public void BaseTypeChange_EmitsRecreateDomain()
        => Linearize(DomainDiff.Modified("app", "d") with { Definition = new DomainType { Name = "d", DataType = SqlType.Int }, DataType = new ValueChange<SqlType>(SqlType.Text, SqlType.Int) })
            .ShouldHaveSingleItem().ShouldBeOfType<RecreateDomain>();

    [Fact]
    public void DefaultAndNotNullChange_EmitInPlaceAlters()
    {
        var plan = Linearize(DomainDiff.Modified("app", "d") with
        {
            Default = new ValueChange<SqlDefaultExpression>(null, "0"),
            NotNull = new ValueChange<bool>(false, true),
        });

        plan.OfType<RecreateDomain>().ShouldBeEmpty();
        plan.OfType<AlterDomainDefault>().ShouldHaveSingleItem().NewDefault.ShouldBe("0");
        plan.OfType<AlterDomainNotNull>().ShouldHaveSingleItem().NotNull.ShouldBeTrue();
    }

    [Fact]
    public void CheckChanges_EmitAddAndDropDomainCheck()
    {
        var plan = Linearize(DomainDiff.Modified("app", "d") with
        {
            Checks = [
            CheckConstraintDiff.Added(new CheckConstraint { Name = "new_chk", Expression = "VALUE > 0" }),
            CheckConstraintDiff.Removed("old_chk"),
        ],
        });

        plan.OfType<AddDomainCheck>().ShouldHaveSingleItem().Check.Name.ShouldBe("new_chk");
        plan.OfType<DropDomainCheck>().ShouldHaveSingleItem().Check.Member.ShouldBe("old_chk");
    }

    [Fact]
    public void DomainCreate_IsOrderedBeforeCreateTable()
    {
        // Arrange
        // A column may use the domain as its type, so the domain must be created first.
        var plan = _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Added("app") with
        {
            Tables = [TableDiff.Added("app", new Table { Name = "t" })],
            Domains = [DomainDiff.Added("app", new DomainType { Name = "d", DataType = SqlType.Text })],
        }]));

        var createDomain = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateDomain).i;

        // Act
        var createTable = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateTable).i;

        // Assert
        createDomain.ShouldBeLessThan(createTable);
    }
}
