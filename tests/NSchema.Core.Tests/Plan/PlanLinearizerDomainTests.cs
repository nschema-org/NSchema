using NSchema.Diff.Model;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Domains;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Tables;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Domains;
using NSchema.Plan.Model.Services;
using NSchema.Plan.Model.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns domain diffs into actions: a base-type change recreates (drop + create), every
/// other facet (default, not-null, checks) alters in place, and domains are ordered before tables / dropped after.
/// </summary>
public sealed class PlanLinearizerDomainTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(DomainDiff domain) =>
        _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", Domains: [domain])]));

    [Fact]
    public void AddedDomain_EmitsCreateDomain()
        => Linearize(new DomainDiff("app", "d", ChangeKind.Add, Definition: new DomainType { Name = "d", DataType = SqlType.Text }))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateDomain>().DomainType.Name.ShouldBe("d");

    [Fact]
    public void BaseTypeChange_EmitsRecreateDomain()
        => Linearize(new DomainDiff("app", "d", ChangeKind.Modify, Definition: new DomainType { Name = "d", DataType = SqlType.Int },
                DataType: new ValueChange<SqlType>(SqlType.Text, SqlType.Int)))
            .ShouldHaveSingleItem().ShouldBeOfType<RecreateDomain>();

    [Fact]
    public void DefaultAndNotNullChange_EmitInPlaceAlters()
    {
        var plan = Linearize(new DomainDiff("app", "d", ChangeKind.Modify,
            Default: new ValueChange<SqlDefaultExpression>(null, "0"),
            NotNull: new ValueChange<bool>(false, true)));

        plan.OfType<RecreateDomain>().ShouldBeEmpty();
        plan.OfType<AlterDomainDefault>().ShouldHaveSingleItem().NewDefault.ShouldBe("0");
        plan.OfType<AlterDomainNotNull>().ShouldHaveSingleItem().NotNull.ShouldBeTrue();
    }

    [Fact]
    public void CheckChanges_EmitAddAndDropDomainCheck()
    {
        var plan = Linearize(new DomainDiff("app", "d", ChangeKind.Modify, Checks:
        [
            new CheckConstraintDiff(ChangeKind.Add, "new_chk", new CheckConstraint { Name = "new_chk", Expression = "VALUE > 0" }),
            new CheckConstraintDiff(ChangeKind.Remove, "old_chk"),
        ]));

        plan.OfType<AddDomainCheck>().ShouldHaveSingleItem().Check.Name.ShouldBe("new_chk");
        plan.OfType<DropDomainCheck>().ShouldHaveSingleItem().Check.Member.ShouldBe("old_chk");
    }

    [Fact]
    public void DomainCreate_IsOrderedBeforeCreateTable()
    {
        // A column may use the domain as its type, so the domain must be created first.
        var plan = _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add,
            Tables: [new TableDiff("app", "t", ChangeKind.Add, Definition: new Table { Name = "t" })],
            Domains: [new DomainDiff("app", "d", ChangeKind.Add, Definition: new DomainType { Name = "d", DataType = SqlType.Text })])]));

        var createDomain = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateDomain).i;
        var createTable = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateTable).i;
        createDomain.ShouldBeLessThan(createTable);
    }
}
