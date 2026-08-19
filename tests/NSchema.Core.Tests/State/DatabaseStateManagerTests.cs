using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.State;
using NSchema.State.Domain;
using NSchema.State.Plugins;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.State;

public sealed class DatabaseStateManagerTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly DatabaseStateSerializer _serializer = new();
    private readonly IDatabaseStateStore _store = Substitute.For<IDatabaseStateStore>();
    private readonly DatabaseStateManager _sut;

    public DatabaseStateManagerTests()
    {
        // A substituted store returns null for Result-typed members (Result has an internal constructor, so
        // NSubstitute cannot auto-create one), so give it a success default each test can override.
        _store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        _store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        _sut = new DatabaseStateManager(_serializer, _store);
    }

    private static DatabaseStateManager Unconfigured() => new(new DatabaseStateSerializer());

    private void StoreHolds(ReadOnlyMemory<byte>? payload) =>
        _store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(payload)));

    [Fact]
    public void IsConfigured_ReflectsWhetherAStoreIsRegistered()
    {
        _sut.IsConfigured.ShouldBeTrue();
        Unconfigured().IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task Read_NoStoreConfigured_IsAFailure()
    {
        // Act
        var result = await Unconfigured().Read(new StateReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(StateDiagnostics.NotConfigured);
    }

    [Fact]
    public async Task Read_NoSnapshotRecordedYet_SucceedsWithANullState()
    {
        // Arrange
        StoreHolds(null);

        // Act
        var result = await _sut.Read(new StateReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBeNull();
    }

    [Fact]
    public async Task Read_ReturnsTheRecordedState()
    {
        // Arrange
        var state = DatabaseState.Empty.RecordExecution([new ScriptExecution(new ScriptReference(null, "seed"), "abc", _now)]);
        StoreHolds(_serializer.Serialize(state));

        // Act
        var result = await _sut.Read(new StateReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldNotBeNull().Scripts.ShouldBe(state.Scripts);
    }

    [Fact]
    public async Task Read_UnreadablePayload_IsAFailure()
    {
        // A corrupt snapshot must fail the read rather than read as empty — a caller writing back an
        // "empty" state would destroy the ledger.
        StoreHolds("not a state payload"u8.ToArray());

        var result = await _sut.Read(new StateReadArguments(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Write_NoStoreConfigured_IsAFailure()
    {
        // Act
        var result = await Unconfigured().Write(new StateWriteArguments(DatabaseState.Empty), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(StateDiagnostics.NotConfigured);
    }

    [Fact]
    public async Task Write_PersistsTheSerializedState_AndReportsThePayloadSize()
    {
        // Arrange
        var state = DatabaseState.Empty.RecordExecution([new ScriptExecution(new ScriptReference(null, "seed"), "abc", _now)]);
        byte[]? written = null;
        await _store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m.ToArray()), Arg.Any<CancellationToken>());

        // Act
        var result = await _sut.Write(new StateWriteArguments(state), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        written.ShouldNotBeNull();
        result.Value.PayloadSize.ShouldBe(written.Length);
        _serializer.Deserialize(written).Scripts.ShouldBe(state.Scripts);
    }

    [Fact]
    public async Task ReadRaw_NoStoreConfigured_IsAFailure()
    {
        // Act
        var result = await Unconfigured().ReadRaw(new StateRawReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadRaw_NoSnapshotRecordedYet_SucceedsWithANullPayload()
    {
        // Arrange
        StoreHolds(null);

        // Act
        var result = await _sut.ReadRaw(new StateRawReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task ReadRaw_ReturnsThePayloadVerbatim_EvenWhenUnreadable()
    {
        // Pulling never interprets the payload, so a corrupt snapshot can still be pulled for repair.
        var payload = "not a state payload"u8.ToArray();
        StoreHolds(payload);

        var result = await _sut.ReadRaw(new StateRawReadArguments(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Payload.ShouldNotBeNull().ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task WriteRaw_ValidPayload_WritesItVerbatim()
    {
        // Arrange — a hand-edited payload is written byte-for-byte, not re-serialized, so nothing the
        // model doesn't understand is silently dropped.
        var payload = _serializer.Serialize(DatabaseState.Empty).ToArray();
        byte[]? written = null;
        await _store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m.ToArray()), Arg.Any<CancellationToken>());

        // Act
        var result = await _sut.WriteRaw(new StateRawWriteArguments(payload), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PayloadSize.ShouldBe(payload.Length);
        written.ShouldBe(payload);
    }

    [Fact]
    public async Task WriteRaw_InvalidPayload_IsAFailure_AndWritesNothing()
    {
        // Act
        var result = await _sut.WriteRaw(new StateRawWriteArguments("not a state payload"u8.ToArray()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("not written");
        await _store.DidNotReceive().Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteRaw_NoStoreConfigured_IsAFailure()
    {
        // Act
        var result = await Unconfigured().WriteRaw(new StateRawWriteArguments(new byte[1]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // --- An unreachable store ---
    //
    // The store reports what it anticipates as a failure; the manager propagates it rather than swallowing it or
    // turning a read into an empty state.

    private void StoreIsUnreachable()
    {
        var unreachable = Diagnostic.Error("state", "could-not-reach-the", "Could not reach the state store: Connection refused");
        _store.Read(Arg.Any<CancellationToken>()).Returns(Result.Failure<StoreReadResult>(unreachable));
        _store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.From(unreachable));
    }

    [Fact]
    public async Task Read_WhenTheStoreIsUnreachable_IsAFailure()
    {
        // Arrange
        StoreIsUnreachable();

        // Act
        var result = await _sut.Read(new StateReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task ReadRaw_WhenTheStoreIsUnreachable_IsAFailure()
    {
        // Arrange
        StoreIsUnreachable();

        // Act
        var result = await _sut.ReadRaw(new StateRawReadArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task Write_WhenTheStoreIsUnreachable_IsAFailure()
    {
        // Arrange
        StoreIsUnreachable();

        // Act
        var result = await _sut.Write(new StateWriteArguments(new DatabaseState(new Database(), [])), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task Read_WhenTheStoreThrows_PropagatesAsADefect()
    {
        // Arrange — the contract is to report what you anticipate; a store that throws instead is broken, and the
        // engine surfaces that as a defect rather than dressing it up as an environmental failure.
        _store.Read(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.Read(new StateReadArguments(), TestContext.Current.CancellationToken));
    }
}
