using NSchema.Diff.Domain;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns composite-type diffs into actions: every change applies in place (no recreate),
/// fields add/drop/retype independently, and composite types are ordered before tables / dropped after.
/// </summary>
public sealed class PlanLinearizerCompositeTypeTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(CompositeTypeDiff type) =>
        _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Containing("app") with { CompositeTypes = [type] }]));

    [Fact]
    public void AddedCompositeType_EmitsCreateCompositeType()
        => Linearize(CompositeTypeDiff.Added("app", new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] }))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateCompositeType>().CompositeType.Name.ShouldBe("address");

    [Fact]
    public void RemovedCompositeType_EmitsDropCompositeType()
        => Linearize(CompositeTypeDiff.Removed("app", "address"))
            .ShouldHaveSingleItem().ShouldBeOfType<DropCompositeType>().Type.Name.ShouldBe("address");

    [Fact]
    public void FieldChanges_EmitInPlaceFieldActions()
    {
        var plan = Linearize(CompositeTypeDiff.Modified("app", "address") with
        {
            Fields = [
            CompositeFieldDiff.Added(new CompositeField("zip", SqlType.Int)),
            CompositeFieldDiff.Removed("old"),
            CompositeFieldDiff.TypeChanged("street", new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(255))),
        ],
        });

        plan.OfType<AddCompositeField>().ShouldHaveSingleItem().Field.Name.ShouldBe("zip");
        plan.OfType<DropCompositeField>().ShouldHaveSingleItem().Field.Member.ShouldBe("old");
        plan.OfType<AlterCompositeFieldType>().ShouldHaveSingleItem().NewType.ShouldBe(SqlType.VarChar(255));
    }

    [Fact]
    public void RenamedCompositeType_EmitsRenameCompositeType()
        => Linearize(CompositeTypeDiff.Modified("app", "address") with { RenamedFrom = "legacy_address" })
            .OfType<RenameCompositeType>().ShouldHaveSingleItem().NewName.ShouldBe("address");

    [Fact]
    public void CommentChange_EmitsSetCompositeTypeComment()
        => Linearize(CompositeTypeDiff.Modified("app", "address") with { Comment = new ValueChange<string>("old", "new") })
            .OfType<SetCompositeTypeComment>().ShouldHaveSingleItem().NewComment.ShouldBe("new");

    [Fact]
    public void CompositeTypeCreate_IsOrderedBeforeCreateTable()
    {
        // Arrange
        // A column may use the composite type as its type, so the type must be created first.
        var plan = _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Added("app") with
        {
            Tables = [TableDiff.Added("app", new Table { Name = "t" })],
            CompositeTypes = [CompositeTypeDiff.Added("app", new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] })],
        }]));

        var createType = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateCompositeType).i;

        // Act
        var createTable = plan.Select((a, i) => (a, i)).Single(x => x.a is CreateTable).i;

        // Assert
        createType.ShouldBeLessThan(createTable);
    }
}
