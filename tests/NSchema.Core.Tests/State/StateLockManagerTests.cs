using NSchema.State.Locks;
using NSchema.State.Locks.Backends;

namespace NSchema.Tests.State;

public sealed class StateLockManagerTests
{
    private readonly RecordingStateLock _stateLock = new();

    private static Task<Result<IStateLockHandle>> Acquire(IStateLock? stateLock, bool skipLock) =>
        ((IStateLockManager)new StateLockManager(stateLock)).Acquire(new AcquireLockArguments("apply") { SkipLock = skipLock }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task NoLockBackend_SucceedsWithTheNoOpHandle_AndSaysNothing()
    {
        // Act
        // An offline run has nothing to lock — a no-op handle, no warning.
        var result = await Acquire(stateLock: null, skipLock: false);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipLock_SucceedsWithTheNoOpHandleAndAWarning_WithoutAcquiring()
    {
        // Act
        var result = await Acquire(_stateLock, skipLock: true);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldHaveSingleItem().Severity.ShouldBe(DiagnosticSeverity.Warning);
        // Peeked to name the lock it ran past, but never acquired.
        _stateLock.Peeks.ShouldBe(1);
        _stateLock.Acquisitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Acquire_SucceedsWithTheRealHandle()
    {
        // Act
        var result = await Acquire(_stateLock, skipLock: false);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(NullStateLockHandle.Instance);
        result.Diagnostics.ShouldBeEmpty();
        _stateLock.Acquisitions.ShouldHaveSingleItem().Operation.ShouldBe("apply");
    }

    [Fact]
    public async Task Contention_IsAFailureCarryingTheHolderDetails()
    {
        // Arrange
        // The lock is already held by another operation — a recoverable, user-facing failure, not a thrown exception.
        _stateLock.OnAcquire = _ => throw new StateLockedException(
            "held", new StateLockInfo("id", "plan", "other@host", DateTimeOffset.UnixEpoch));

        // Act
        var result = await Acquire(_stateLock, skipLock: false);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldSatisfyAllConditions(
            m => m.ShouldContain("locked by other@host"),
            m => m.ShouldContain("--no-lock"));
    }

    [Fact]
    public async Task ReturnedHandle_ReleasesTheLock()
    {
        // Arrange
        // Release is explicit (the handle is not disposable — a manual lock can outlive the process).
        var handle = (await Acquire(_stateLock, skipLock: false)).Value.ShouldNotBeNull();

        // Act
        await handle.Release(TestContext.Current.CancellationToken);

        // Assert
        _stateLock.Released.ShouldBe(1);
    }

    [Fact]
    public async Task Peek_NoLockBackend_ReturnsNull()
    {
        // Nothing to peek when the state is unlockable — reads the same as free.
        var info = await new StateLockManager(stateLock: null).Peek(TestContext.Current.CancellationToken);

        info.ShouldBeNull();
    }

    [Fact]
    public async Task Peek_ReadsTheHolderWithoutAcquiring()
    {
        _stateLock.PeekResult = new StateLockInfo("id", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        var info = await new StateLockManager(_stateLock).Peek(TestContext.Current.CancellationToken);

        info.ShouldNotBeNull().Who.ShouldBe("tom@dev");
        _stateLock.Peeks.ShouldBe(1);
        _stateLock.Acquisitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Acquire_CreatesLockInfoFromArguments()
    {
        // Arrange
        var request = new AcquireLockArguments("manual") { TimeToLive = TimeSpan.FromMinutes(30) };

        // Act
        await new StateLockManager(_stateLock).Acquire(request, TestContext.Current.CancellationToken);

        // Assert
        var acquired = _stateLock.Acquisitions.ShouldHaveSingleItem();
        acquired.Operation.ShouldBe("manual");
        acquired.Id.Value.ShouldNotBeNullOrEmpty();
        acquired.Who.ShouldBe(LockHolder.Current());
        (acquired.ExpiresUtc!.Value - acquired.CreatedUtc).ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Release_NoLockBackend_ReturnsNull()
    {
        var released = await new StateLockManager(stateLock: null).Release(TestContext.Current.CancellationToken);

        released.ShouldBeNull();
    }

    [Fact]
    public async Task Release_WhenHeld_ForceReleasesAndReturnsTheReleasedLock()
    {
        _stateLock.PeekResult = new StateLockInfo("id", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        var released = await new StateLockManager(_stateLock).Release(TestContext.Current.CancellationToken);

        released.ShouldNotBeNull().Who.ShouldBe("tom@dev");
        _stateLock.ForceReleases.ShouldBe(1);
    }

    [Fact]
    public async Task Release_WhenFree_ReturnsNull_WithoutReleasing()
    {
        // Nothing is held (the default PeekResult is null), so there is nothing to remove.
        var released = await new StateLockManager(_stateLock).Release(TestContext.Current.CancellationToken);

        released.ShouldBeNull();
        _stateLock.ForceReleases.ShouldBe(0);
    }
}
