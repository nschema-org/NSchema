using Microsoft.Extensions.Options;
using NSchema.State.Locks;
using NSchema.State.Locks.Plugins;

namespace NSchema.Tests.State;

public sealed class FileStateLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nschema-lock-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly FileStateLock _sut;

    public FileStateLockTests()
    {
        _path = Path.Combine(_directory, "nested", "state.lock");
        _sut = new FileStateLock(Options.Create(new FileStateLockOptions { Path = _path }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Acquire_CreatesLockFileAndMissingDirectories()
    {
        // Arrange
        var lockInfo = Lock("apply");

        // Act
        var handle = (await _sut.Acquire(lockInfo, TestContext.Current.CancellationToken)).Require();

        // Assert
        File.Exists(_path).ShouldBeTrue();
        handle.Info.ShouldBe(lockInfo);
        (await _sut.Peek(TestContext.Current.CancellationToken)).Require().Held.ShouldBe(lockInfo);
    }

    [Fact]
    public async Task Acquire_WithoutTimeToLive_RecordsNoExpiry()
    {
        // Act
        var handle = (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        // Assert
        handle.Info.ExpiresUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Acquire_WithTimeToLive_RecordsExpiryRelativeToCreation()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(30);

        // Act
        var handle = (await _sut.Acquire(Lock("manual", ttl), TestContext.Current.CancellationToken)).Require();

        // Assert
        handle.Info.ExpiresUtc.ShouldNotBeNull();
        // The expiry is the creation time plus the requested lifetime.
        (handle.Info.ExpiresUtc.Value - handle.Info.CreatedUtc).ShouldBe(ttl);
    }

    [Fact]
    public async Task Acquire_WithTimeToLive_PersistsExpiryForLaterReaders()
    {
        // Arrange
        await _sut.Acquire(Lock("manual", TimeSpan.FromMinutes(30)), TestContext.Current.CancellationToken);

        // Act
        // A separate reader (e.g. lock status in another process) sees the recorded expiry.
        var peeked = (await _sut.Peek(TestContext.Current.CancellationToken)).Require().Held;

        // Assert
        peeked.ShouldNotBeNull();
        peeked.ExpiresUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Release_ReleasesLockFile()
    {
        // Arrange
        var handle = (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        // Act
        await handle.Release(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_path).ShouldBeFalse();
    }

    [Fact]
    public async Task Acquire_WhenAlreadyHeld_ThrowsWithHolderInfo()
    {
        // Arrange
        (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        var ex = await Should.ThrowAsync<StateLockedException>(

            // Act
            () => _sut.Acquire(Lock("destroy")));

        // Assert
        ex.ExistingLock.ShouldNotBeNull();
        ex.ExistingLock.Operation.ShouldBe("apply");
        ex.Message.ShouldContain(_path);
    }

    [Fact]
    public async Task Acquire_AfterRelease_Succeeds()
    {
        // Arrange
        var first = (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();
        await first.Release(TestContext.Current.CancellationToken);

        // Act
        // Should not throw now that the first lock is released.
        (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        // Assert
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Release_IsIdempotent()
    {
        var handle = (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        await handle.Release(TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(async () => await handle.Release(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NeverReleasing_LeavesTheLockHeld()
    {
        // Arrange
        // A manual hold acquires and intentionally never releases, so the lock outlives the handle.
        (await _sut.Acquire(Lock("manual"), TestContext.Current.CancellationToken)).Require();

        File.Exists(_path).ShouldBeTrue();

        // Act
        var stillHeld = await _sut.Peek(TestContext.Current.CancellationToken);

        // Assert
        stillHeld.ShouldNotBeNull();
    }

    [Fact]
    public async Task Release_RemovesAHeldLock()
    {
        // Arrange
        // A handle is held but we forcibly release it (as if from another process recovering a stale lock).
        (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        // Act
        await _sut.Release(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_path).ShouldBeFalse();
        (await _sut.Peek(TestContext.Current.CancellationToken)).Require().Held.ShouldBeNull();
    }

    [Fact]
    public async Task Release_WhenNothingHeld_DoesNothing()
        => await Should.NotThrowAsync(async () => await _sut.Release(TestContext.Current.CancellationToken));

    [Fact]
    public async Task Release_ThenAcquire_Succeeds()
    {
        // Arrange
        (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();
        await _sut.Release(TestContext.Current.CancellationToken);

        // Act
        (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();

        // Assert
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Release_DoesNotDeleteALockHeldByAnother()
    {
        // Arrange
        // Acquire, then simulate a force-unlock (the file is removed by hand) and a fresh acquire by another
        // holder. Releasing the first handle must leave the second holder's lock alone — the file now records a
        // different lock id.
        var first = (await _sut.Acquire(Lock("apply"), TestContext.Current.CancellationToken)).Require();
        File.Delete(_path);
        var second = (await _sut.Acquire(Lock("destroy"), TestContext.Current.CancellationToken)).Require();

        // Act
        await first.Release(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_path).ShouldBeTrue();
        await second.Release(TestContext.Current.CancellationToken);
    }

    private static StateLockInfo Lock(string operation, TimeSpan? timeToLive = null)
    {
        var createdUtc = DateTimeOffset.UtcNow;
        return new StateLockInfo(
            LockId.New(),
            operation,
            LockHolder.Current(),
            createdUtc,
            timeToLive is { } ttl ? createdUtc + ttl : null);
    }
}
