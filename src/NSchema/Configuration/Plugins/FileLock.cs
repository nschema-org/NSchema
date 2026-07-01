using System.Diagnostics;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A best-effort exclusive, cross-process advisory lock backed by a held-open lock file.
/// </summary>
/// <remarks>
/// Opening the file with <see cref="FileShare.None"/> is the lock: .NET enforces the share mode across processes,
/// and across independent handles within a single process too.
/// </remarks>
internal sealed class FileLock : IDisposable
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(100);

    private readonly FileStream _stream;

    private FileLock(FileStream stream) => _stream = stream;

    /// <summary>
    /// Acquires the lock at <paramref name="path"/>, polling until it is free, or returns <see langword="null"/> if it
    /// is still held after <paramref name="timeout"/>. The parent directory must already exist.
    /// </summary>
    public static FileLock? Acquire(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException)
            {
                // The lock is held by someone else (a sharing violation surfaces as IOException on every platform).
                if (stopwatch.Elapsed >= timeout)
                {
                    return null;
                }

                Thread.Sleep(_pollInterval);
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
