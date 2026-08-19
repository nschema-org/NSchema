using NSchema.Deployment;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.State;
using NSchema.State.Domain;
using NSchema.State.Plugins;

namespace NSchema.Tests.Operations;

public sealed class DriftOperationTests
{
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly IDatabaseStateStore _store = Substitute.For<IDatabaseStateStore>();
    private readonly IDatabaseStateSerializer _serializer = new DatabaseStateSerializer();
    private readonly IDatabaseComparer _comparer = Substitute.For<IDatabaseComparer>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    private readonly Database _recorded = new Database { Schemas = [new Schema { Name = "app" }] };
    private readonly Database _live = new Database { Schemas = [new Schema { Name = "app" }, new Schema { Name = "audit" }] };
    private readonly DatabaseDiff _diff = new([SchemaDiff.Added("audit")]);

    private readonly DriftOperation _sut;

    public DriftOperationTests()
    {
        // Recorded state comes from the store; live comes from the provider.
        _store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(_serializer.Serialize(new DatabaseState(_recorded)))));
        _provider.GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_live));
        _comparer.Compare(Arg.Any<AlignedDatabase>(), Arg.Any<Database>()).Returns(_diff);

        _sut = new DriftOperation(_provider, new DatabaseStateManager(_serializer, _store), _comparer, _progress);
    }

    private static DriftArguments Args(PlanningScope? scope = null) => new() { Scope = scope ?? PlanningScope.All };

    [Fact]
    public async Task Execute_DiffsRecordedAgainstLive()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — direction is recorded -> live, so the diff describes how live drifted from the recorded state.
        // The recorded database round-trips through the state store, so match it by content, not reference.
        _comparer.Received(1).Compare(
            Arg.Is<AlignedDatabase>(a => a!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })), _live);
    }

    [Fact]
    public async Task Execute_ReadsRecordedFromState_AndLiveFromProvider()
    {
        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        await _store.Received(1).Read(Arg.Any<CancellationToken>());
        await _provider.Received(1).GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsScopeToLive_AndFiltersRecorded()
    {
        // Arrange — recorded state holds an out-of-scope schema; the scoped read must drop it before diffing.
        _store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_serializer.Serialize(new DatabaseState(new Database { Schemas = [new Schema { Name = "app" }, new Schema { Name = "other" }] })))));

        // Act
        await _sut.Execute(Args(PlanningScope.To(DatabaseAddress.Schema("app"))), TestContext.Current.CancellationToken);

        // Assert — live gets the scope directly; recorded is filtered to it before the comparer sees it.
        await _provider.Received(1).GetDatabase(Arg.Is<PlanningScope>(s => s!.Contains("app") && !s.IsUnscoped), Arg.Any<CancellationToken>());
        _comparer.Received(1).Compare(Arg.Is<AlignedDatabase>(a => a!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })), _live);
    }

    [Fact]
    public async Task Execute_NoRecordedState_DiffsAgainstAnEmptyDatabase()
    {
        // Arrange — before anything is recorded, drift measures the whole live database as new.
        _store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));

        // Act
        await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert — the recorded side is an empty database.
        _comparer.Received(1).Compare(Arg.Is<AlignedDatabase>(a => a!.Database.Schemas.Count == 0), _live);
    }

    [Fact]
    public async Task Execute_ReturnsTheComparerDiff_AndReportsDrift()
    {
        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(_diff);
        result.Value!.HasDrift.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_WhenDiffEmpty_ReportsNoDrift()
    {
        // Arrange
        _comparer.Compare(Arg.Any<AlignedDatabase>(), Arg.Any<Database>()).Returns(new DatabaseDiff([]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasDrift.ShouldBeFalse();
    }
}
