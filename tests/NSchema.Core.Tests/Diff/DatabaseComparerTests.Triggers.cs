using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Triggers;
using NSchema.Model.Columns;
using NSchema.Model.Routines;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Triggers (table members)
    // -------------------------------------------------------------------------

    private IReadOnlyList<TriggerDiff> DiffTriggers(IReadOnlyList<Trigger> current, IReadOnlyList<Trigger> desired) =>
        DiffTable(
            new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }], Triggers = [.. current] },
            new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }], Triggers = [.. desired] })?.Triggers ?? [];

    private static Trigger AfterInsert(string name, string function = "log", string? comment = null) =>
        new Trigger
        {
            Name = name,
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Insert,
            Function = new RoutineReference("app", function),
            Level = TriggerLevel.Row,
            Comment = comment,
        };

    [Fact]
    public void Compare_NewTrigger_IsAddCarryingDefinition()
    {
        var diff = DiffTriggers([], [AfterInsert("audit")]).ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Add);
        diff.Definition!.Function.ShouldBe("app.log");
        diff.Definition.Events.ShouldBe(TriggerEvent.Insert);
    }

    [Fact]
    public void Compare_RemovedTrigger_IsRemove()
    {
        var diff = DiffTriggers([AfterInsert("audit")], []).ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedTrigger_ProducesNoDiff()
        => DiffTriggers([AfterInsert("audit")], [AfterInsert("audit")]).ShouldBeEmpty();

    [Fact]
    public void Compare_TriggerCommentOnlyChange_IsModifyInPlace()
    {
        // Act
        // Equality excludes the comment, so a comment-only change is a single in-place modify, not a recreate.
        var diff = DiffTriggers([AfterInsert("audit", comment: "old")], [AfterInsert("audit", comment: "new")])

        // Assert
            .ShouldHaveSingleItem();

        diff.Kind.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_TriggerStructuralChange_IsRemoveThenAdd()
    {
        // Arrange
        // Triggers don't rename or alter; a timing change is a drop + recreate, like an index.
        var current = new Trigger { Name = "audit", Timing = TriggerTiming.Before, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log") };
        var desired = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log") };

        // Act
        var diffs = DiffTriggers([current], [desired]);

        // Assert
        diffs.Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }

    [Fact]
    public void Compare_UpdateOfColumnsChange_IsStructural()
    {
        var current = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Update, Function = new RoutineReference("app", "log"), UpdateOfColumns = ["a"] };
        var desired = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Update, Function = new RoutineReference("app", "log"), UpdateOfColumns = ["a", "b"] };

        DiffTriggers([current], [desired]).Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }

    [Fact]
    public void Compare_TriggerBodyChange_IsStructural()
    {
        // An inline-body change is part of structural equality, so it is a drop + recreate (not a comment-only modify).
        var current = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "BEGIN SELECT 1 END" };
        var desired = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "BEGIN SELECT 2 END" };

        DiffTriggers([current], [desired]).Select(d => d.Kind).ShouldBe([ChangeKind.Remove, ChangeKind.Add], ignoreOrder: true);
    }
}
