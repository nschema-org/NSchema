using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Sequences;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given sequences, returning the single sequence diff (null when unchanged).</summary>
    private SequenceDiff? DiffSequences(IReadOnlyList<Sequence> current, IReadOnlyList<Sequence> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Sequences = [.. current] }), Db(new Schema { Name = "app", Sequences = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.Sequences.SingleOrDefault();

    [Fact]
    public void Compare_NewSequence_IsAddCarryingDefinition()
    {
        var diff = DiffSequences([], [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 100) }]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public void Compare_RemovedSequence_IsRemove()
    {
        var diff = DiffSequences([new Sequence { Name = "order_id" }], []);

        diff!.Change.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UnchangedSequence_ProducesNoDiff()
        => DiffSequences(
            [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1, IncrementBy: 5) }],
            [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1, IncrementBy: 5) }]).ShouldBeNull();

    [Fact]
    public void Compare_SequenceWithNullAndEmptyOptions_ProducesNoDiff()
        // The model normalizes null options to an empty set, so the two spellings never read as a change.
        => DiffSequences([new Sequence { Name = "order_id" }], [new Sequence { Name = "order_id", Options = new SequenceOptions() }]).ShouldBeNull();

    [Fact]
    public void Compare_RenamedSequence_SetsRenamedFrom()
    {
        var diff = DiffSequences(
            [new Sequence { Name = "bill_id" }],
            [new Sequence { Name = "invoice_id" }],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.Sequence("app", "bill_id"), "invoice_id")]));

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("bill_id");
        diff.Name.ShouldBe("invoice_id");
        diff.Options.ShouldBeNull(); // options unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_SequenceCommentOnlyChange_IsModify()
    {
        var diff = DiffSequences(
            [new Sequence { Name = "order_id", Comment = "old" }],
            [new Sequence { Name = "order_id", Comment = "new" }]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Options.ShouldBeNull();
    }

    [Fact]
    public void Compare_SequenceOptionsChange_CarriesOldAndNewOptions()
    {
        var diff = DiffSequences(
            [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1, IncrementBy: 1) }],
            [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true) }]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Options.ShouldBe(new ValueChange<SequenceOptions>(
            new SequenceOptions(StartWith: 1, IncrementBy: 1),
            new SequenceOptions(StartWith: 1, IncrementBy: 5, Cycle: true)));
    }
}
