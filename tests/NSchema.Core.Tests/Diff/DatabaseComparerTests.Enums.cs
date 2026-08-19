using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Enums;
using NSchema.Model;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given enums, returning the single enum diff (null when unchanged).</summary>
    private EnumDiff? DiffEnums(IReadOnlyList<EnumType> current, IReadOnlyList<EnumType> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Enums = [.. current] }), Db(new Schema { Name = "app", Enums = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.Enums.SingleOrDefault();

    [Fact]
    public void Compare_NewEnum_IsAddCarryingDefinition()
    {
        var diff = DiffEnums([], [new EnumType { Name = "status", Values = ["a", "b"] }]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.Values.ShouldBe(["a", "b"]);
        diff.AddedValues.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_RemovedEnum_IsRemove()
    {
        var diff = DiffEnums([new EnumType { Name = "status", Values = ["a"] }], []);

        diff!.Change.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedEnum_ProducesNoDiff()
        => DiffEnums([new EnumType { Name = "status", Values = ["a", "b"] }], [new EnumType { Name = "status", Values = ["a", "b"] }]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedEnum_SetsRenamedFrom()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "state", Values = ["a"] }],
            [new EnumType { Name = "status", Values = ["a"] }],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.Enum("app", "state"), "status")]));

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("state");
        diff.Name.ShouldBe("status");
        diff.Values.ShouldBeNull(); // values unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_EnumCommentOnlyChange_IsModify()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a"], Comment = "old" }],
            [new EnumType { Name = "status", Values = ["a"], Comment = "new" }]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Values.ShouldBeNull();
    }

    [Fact]
    public void Compare_EnumAppendedValue_AnchorsAfterThePreviousValue()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a", "b"] }],
            [new EnumType { Name = "status", Values = ["a", "b", "c"] }]);

        diff!.AddedValues.ShouldHaveSingleItem().ShouldBe(new EnumValueAddition("c", After: "b"));
        diff.RequiresRecreate.ShouldBeFalse();
        diff.Values!.Old.ShouldBe(["a", "b"]);
        diff.Values.New.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Compare_EnumHeadInsertion_AnchorsBeforeTheFirstExistingValue()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["c"] }],
            [new EnumType { Name = "status", Values = ["a", "b", "c"] }]);

        // a goes before the only existing value; b then chains after a, which exists once a is added.
        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("a", Before: "c"),
            new EnumValueAddition("b", After: "a"),
        ]);
    }

    [Fact]
    public void Compare_EnumMiddleInsertion_AnchorsAfterThePrecedingValue()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a", "c"] }],
            [new EnumType { Name = "status", Values = ["a", "b", "c"] }]);

        diff!.AddedValues.ShouldHaveSingleItem().ShouldBe(new EnumValueAddition("b", After: "a"));
    }

    [Fact]
    public void Compare_EnumConsecutiveInsertions_ChainTheirAnchors()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a"] }],
            [new EnumType { Name = "status", Values = ["a", "b", "c"] }]);

        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("b", After: "a"),
            new EnumValueAddition("c", After: "b"),
        ]);
    }

    [Fact]
    public void Compare_EnumWithEmptyCurrent_AppendsWithoutAnchors()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status" }],
            [new EnumType { Name = "status", Values = ["a", "b"] }]);

        diff!.AddedValues.ShouldBe([
            new EnumValueAddition("a"),
            new EnumValueAddition("b", After: "a"),
        ]);
    }

    [Fact]
    public void Compare_EnumValueRemoval_RequiresRecreate()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a", "b", "c"] }],
            [new EnumType { Name = "status", Values = ["a", "c"] }]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.AddedValues.ShouldBeEmpty();
        diff.RequiresRecreate.ShouldBeTrue();
        // The old and new lists are still recorded so drift can display the change.
        diff.Values!.Old.ShouldBe(["a", "b", "c"]);
        diff.Values.New.ShouldBe(["a", "c"]);
    }

    [Fact]
    public void Compare_EnumValueReorder_RequiresRecreate()
    {
        var diff = DiffEnums(
            [new EnumType { Name = "status", Values = ["a", "b"] }],
            [new EnumType { Name = "status", Values = ["b", "a"] }]);

        diff!.AddedValues.ShouldBeEmpty();
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_EnumValues_AreCaseSensitive()
        => DiffEnums([new EnumType { Name = "status", Values = ["Active"] }], [new EnumType { Name = "status", Values = ["active"] }])!
            .RequiresRecreate.ShouldBeTrue();

}
