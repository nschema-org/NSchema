using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Triggers;
using NSchema.Model.Columns;
using NSchema.Model.Routines;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins trigger ordering: a trigger is created after its table (and after the function it calls, which already
/// precedes tables) and dropped before its table.
/// </summary>
public sealed class PlanLinearizerTriggerTests
{
    private readonly PlanLinearizer _linearizer = new();

    private static int IndexOf<T>(IReadOnlyList<MigrationAction> actions) =>
        actions.Select((a, i) => (a, i)).First(x => x.a is T).i;

    [Fact]
    public void CreateTrigger_IsEmittedAfterItsTableIsCreated()
    {
        var trigger = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log") };
        var table = TableDiff.Added("app", new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }) with
        {
            Triggers = [TriggerDiff.Added(trigger)],
        };

        var actions = _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Added("app") with { Tables = [table] }]), PlanDependencies.None, DialectCapabilities.Standard);

        IndexOf<CreateTrigger>(actions).ShouldBeGreaterThan(IndexOf<CreateTable>(actions));
    }

    [Fact]
    public void DropTrigger_IsEmittedBeforeTablesAreDropped()
    {
        var modified = TableDiff.Modified("app", "users") with { Triggers = [TriggerDiff.Removed("audit")] };
        var dropped = TableDiff.Removed("app", "legacy");

        var actions = _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [modified, dropped] }]), PlanDependencies.None, DialectCapabilities.Standard);

        IndexOf<DropTrigger>(actions).ShouldBeLessThan(IndexOf<DropTable>(actions));
    }

    [Fact]
    public void AddedTrigger_WithComment_EmitsCreateThenSetComment()
    {
        var trigger = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "log"), Comment = "note" };
        var table = TableDiff.Added("app", new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }) with
        {
            Triggers = [
                TriggerDiff.Added(trigger),
                TriggerDiff.CommentChanged("audit", new ValueChange<string>(null, "note")),
            ],
        };

        var actions = _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Added("app") with { Tables = [table] }]), PlanDependencies.None, DialectCapabilities.Standard);

        actions.OfType<CreateTrigger>().ShouldHaveSingleItem().Trigger.Name.ShouldBe("audit");
        actions.OfType<SetTriggerComment>().ShouldHaveSingleItem().NewComment.ShouldBe("note");
    }
}
