using NSchema.Model;
using NSchema.State.Domain;

namespace NSchema.Tests.State;

public sealed class DatabaseStateTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordScripts_AddsEntriesToTheLedger()
    {
        // Arrange
        var state = DatabaseState.Empty;

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().ShouldBe(new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now));
    }

    [Fact]
    public void RecordScripts_ReplacesAnEarlierExecutionByName()
    {
        // Arrange
        var state = new DatabaseState(new Database(), [new ScriptExecution(new ScopedAddress(null, "seed"), "old", DateTimeOffset.UnixEpoch)]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScopedAddress(null, "seed"), "new", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().Hash.ShouldBe("new");
    }

    [Fact]
    public void RecordScripts_LeavesOtherEntriesAlone()
    {
        // Arrange
        var existing = new ScriptExecution(new ScopedAddress(null, "api-login"), "hash", DateTimeOffset.UnixEpoch);
        var state = new DatabaseState(new Database(), [existing]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldBe([existing, new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now)]);
    }

    [Fact]
    public void RecordScripts_NothingExecuted_ReturnsTheSameState()
        => DatabaseState.Empty.RecordExecution([]).ShouldBeSameAs(DatabaseState.Empty);

    [Fact]
    public void FindScript_MatchesByExactName()
    {
        // Arrange
        var existing = new ScriptExecution(new ScopedAddress(null, "Seed"), "abc", _now);
        var state = new DatabaseState(new Database(), [existing]);

        // Assert — identifiers are case-sensitive, so only the exact name finds the entry.
        state.FindExecution(new ScopedAddress(null, "Seed")).ShouldBe(existing);
        state.FindExecution(new ScopedAddress(null, "seed")).ShouldBeNull();
    }

    [Fact]
    public void FindScript_NothingRecordedUnderTheName_ReturnsNull()
        => DatabaseState.Empty.FindExecution(new ScopedAddress(null, "seed")).ShouldBeNull();

    [Fact]
    public void FindScript_SameNameInAnotherScope_ReturnsNull()
    {
        // Arrange — identity is (scope, name): a scoped execution is not found by the global address, nor by
        // another schema's.
        var scoped = new ScriptExecution(new ScopedAddress("sales", "seed"), "abc", _now);
        var state = new DatabaseState(new Database(), [scoped]);

        // Assert
        state.FindExecution(new ScopedAddress(null, "seed")).ShouldBeNull();
        state.FindExecution(new ScopedAddress("billing", "seed")).ShouldBeNull();
        state.FindExecution(new ScopedAddress("sales", "seed")).ShouldBe(scoped);
    }

    [Fact]
    public void RecordScripts_SameNameInAnotherScope_DoesNotReplace()
    {
        // Arrange
        var global = new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now);
        var state = new DatabaseState(new Database(), [global]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScopedAddress("sales", "seed"), "def", _now)]);

        // Assert
        recorded.Scripts.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveScript_RemovesTheEntryByExactName()
    {
        // Arrange
        var other = new ScriptExecution(new ScopedAddress(null, "api-login"), "hash", _now);
        var state = new DatabaseState(new Database(), [new ScriptExecution(new ScopedAddress(null, "seed"), "abc", _now), other]);

        // Act
        var removed = state.RemoveExecution(new ScopedAddress(null, "seed"));

        // Assert
        removed.Scripts.ShouldBe([other]);
    }

    [Fact]
    public void RemoveScript_NothingRecordedUnderTheName_ReturnsTheSameState()
        => DatabaseState.Empty.RemoveExecution(new ScopedAddress(null, "seed")).ShouldBeSameAs(DatabaseState.Empty);
}
