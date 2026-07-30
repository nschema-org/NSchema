using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Domains;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given domains, returning the single domain diff (null when unchanged).</summary>
    private DomainDiff? DiffDomains(IReadOnlyList<DomainType> current, IReadOnlyList<DomainType> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Domains = [.. current] }), Db(new Schema { Name = "app", Domains = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.Domains.SingleOrDefault();

    [Fact]
    public void Compare_NewDomain_IsAddCarryingDefinition()
    {
        var diff = DiffDomains([], [new DomainType { Name = "typeid", DataType = SqlType.Text, Default = "''", NotNull = true }]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.DataType.ShouldBe(SqlType.Text);
        diff.Definition.NotNull.ShouldBeTrue();
    }

    [Fact]
    public void Compare_RemovedDomain_IsRemove()
        => DiffDomains([new DomainType { Name = "typeid", DataType = SqlType.Text }], [])!.Change.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedDomain_ProducesNoDiff()
        => DiffDomains([new DomainType { Name = "typeid", DataType = SqlType.Text }], [new DomainType { Name = "typeid", DataType = SqlType.Text }]).ShouldBeNull();

    [Fact]
    public void Compare_BaseTypeChange_RequiresRecreate()
    {
        // Postgres has no ALTER DOMAIN … TYPE, so a base-type change drops + recreates.
        var diff = DiffDomains([new DomainType { Name = "d", DataType = SqlType.Text }], [new DomainType { Name = "d", DataType = SqlType.VarChar(255) }]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.DataType.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255)));
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull(); // the desired domain rides along for the recreate
    }

    [Fact]
    public void Compare_DefaultChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainType { Name = "d", DataType = SqlType.Text, Default = "'a'" }], [new DomainType { Name = "d", DataType = SqlType.Text, Default = "'b'" }]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Default.ShouldBe(new ValueChange<SqlDefaultExpression>("'a'", "'b'"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_NotNullChange_IsInPlace()
    {
        var diff = DiffDomains([new DomainType { Name = "d", DataType = SqlType.Text }], [new DomainType { Name = "d", DataType = SqlType.Text, NotNull = true }]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.NotNull.ShouldBe(new ValueChange<bool>(false, true));
    }

    [Fact]
    public void Compare_CheckAddedAndRemoved_AreInPlace()
    {
        var diff = DiffDomains(
            [new DomainType { Name = "d", DataType = SqlType.Text, Checks = [new CheckConstraint { Name = "old_chk", Expression = "VALUE <> ''" }] }],
            [new DomainType { Name = "d", DataType = SqlType.Text, Checks = [new CheckConstraint { Name = "new_chk", Expression = "length(VALUE) > 0" }] }]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Checks.Select(c => (c.Change, c.Name.Value)).ShouldBe(
            [(ChangeKind.Remove, "old_chk"), (ChangeKind.Add, "new_chk")], ignoreOrder: true);
    }

    [Fact]
    public void Compare_RenamedDomain_SetsRenamedFrom()
    {
        var diff = DiffDomains([new DomainType { Name = "old_d", DataType = SqlType.Text }], [new DomainType { Name = "d", DataType = SqlType.Text }],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(App("old_d") with { Kind = SchemaObjectKind.Domain }, "d")]));

        diff!.RenamedFrom.ShouldBe("old_d");
        diff.RequiresRecreate.ShouldBeFalse(); // a rename is in place, not a recreate
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModify()
    {
        var diff = DiffDomains([new DomainType { Name = "d", DataType = SqlType.Text, Comment = "old" }], [new DomainType { Name = "d", DataType = SqlType.Text, Comment = "new" }]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }
}
