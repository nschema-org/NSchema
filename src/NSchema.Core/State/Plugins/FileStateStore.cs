using Microsoft.Extensions.Options;

namespace NSchema.State.Plugins;

/// <summary>
/// An <see cref="IDatabaseStateStore"/> that persists the database snapshot to a file on the local filesystem.
/// </summary>
/// <param name="options">The absolute or relative path of the state file.</param>
/// <remarks>
/// Useful for local development and for backends that surface state as a mounted file.
/// </remarks>
internal sealed class FileDatabaseStateStore(IOptions<FileDatabaseStateStoreOptions> options) : IDatabaseStateStore
{
    /// <inheritdoc />
    public async Task<Result<StoreReadResult>> Read(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.Value.Path))
        {
            return new StoreReadResult(null);
        }

        try
        {
            return new StoreReadResult(await File.ReadAllBytesAsync(options.Value.Path, cancellationToken));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<StoreReadResult>(StateDiagnostics.Unreachable(ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(options.Value.Path);
        var directory = Path.GetDirectoryName(path);

        // Write to a sibling temp file and atomically rename it into place, so a concurrent reader (e.g. a plan reading
        // the recorded state while an apply captures new state) never observes a half-written file. The temp must live
        // in the same directory as the target so the move is a same-volume rename rather than a copy.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(temp, state, cancellationToken);
            File.Move(temp, path, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return Result.From(StateDiagnostics.Unreachable(ex));
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    // Best-effort cleanup of a temp file left behind by a failed write; a stray temp file is harmless either way.
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore: we are already unwinding from the original failure, which is the one that matters.
        }
    }
}
