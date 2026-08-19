using NSchema.Diff.Domain;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Schemas;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Composite types
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given composite types, returning the single diff (null when unchanged).</summary>
    private CompositeTypeDiff? DiffCompositeTypes(IReadOnlyList<CompositeType> current, IReadOnlyList<CompositeType> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", CompositeTypes = [.. current] }), Db(new Schema { Name = "app", CompositeTypes = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.CompositeTypes.SingleOrDefault();

    private static CompositeType Address(params CompositeField[] fields) => new CompositeType { Name = "address", Fields = [.. fields] };

    [Fact]
    public void Compare_NewCompositeType_IsAddCarryingDefinition()
    {
        var diff = DiffCompositeTypes([], [Address(new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int))]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.Fields.Count.ShouldBe(2);
    }

    [Fact]
    public void Compare_RemovedCompositeType_IsRemove()
        => DiffCompositeTypes([Address(new CompositeField("street", SqlType.Text))], [])!.Change.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedCompositeType_ProducesNoDiff()
        => DiffCompositeTypes(
            [Address(new CompositeField("street", SqlType.Text))],
            [Address(new CompositeField("street", SqlType.Text))]).ShouldBeNull();

    [Fact]
    public void Compare_AddedField_IsInPlaceAdd()
    {
        var diff = DiffCompositeTypes(
            [Address(new CompositeField("street", SqlType.Text))],
            [Address(new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int))]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        var field = diff.Fields.ShouldHaveSingleItem();
        field.Change.ShouldBe(ChangeKind.Add);
        field.Name.ShouldBe("zip");
        field.Definition!.DataType.ShouldBe(SqlType.Int);
    }

    [Fact]
    public void Compare_RemovedField_IsInPlaceRemove()
    {
        var diff = DiffCompositeTypes(
            [Address(new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int))],
            [Address(new CompositeField("street", SqlType.Text))]);

        var field = diff!.Fields.ShouldHaveSingleItem();
        field.Change.ShouldBe(ChangeKind.Remove);
        field.Name.ShouldBe("zip");
    }

    [Fact]
    public void Compare_RetypedField_IsInPlaceModify()
    {
        var diff = DiffCompositeTypes(
            [Address(new CompositeField("zip", SqlType.Text))],
            [Address(new CompositeField("zip", SqlType.Int))]);

        var field = diff!.Fields.ShouldHaveSingleItem();
        field.Change.ShouldBe(ChangeKind.Modify);
        field.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Text, SqlType.Int));
    }

    [Fact]
    public void Compare_RenamedCompositeType_SetsRenamedFrom()
    {
        var diff = DiffCompositeTypes(
            [new CompositeType { Name = "legacy_address", Fields = [new CompositeField("street", SqlType.Text)] }],
            [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] }],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.CompositeType("app", "legacy_address"), "address")]));

        diff!.RenamedFrom.ShouldBe("legacy_address");
    }

    [Fact]
    public void Compare_CompositeType_CommentOnlyChange_IsModify()
    {
        var diff = DiffCompositeTypes(
            [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)], Comment = "old" }],
            [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)], Comment = "new" }]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }
}
