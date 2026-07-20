using System.CommandLine;
using System.Text;
using NSchema.Configuration;
using NSchema.State.Locks;

namespace NSchema.Commands.Lock;

/// <summary>
/// Composes the copy/paste <c>nschema lock release</c> command suggested while a lock is held.
/// </summary>
internal static class LockReleaseHint
{
    /// <summary>
    /// Builds the release command for <paramref name="lockId"/>, carrying the arguments that select which lock
    /// is targeted (<c>--environment</c> and <c>--directory</c>) so pasting it verbatim releases the right one.
    /// </summary>
    public static string Command(LockId lockId, string? environment, ParseResult parseResult)
    {
        var sb = new StringBuilder("nschema lock release ").Append(lockId);
        if (!string.IsNullOrEmpty(environment))
        {
            sb.Append(" --environment ").Append(Quote(environment));
        }
        if (CommonOptions.Directory.TryGetValue(parseResult, out var directory))
        {
            sb.Append(" --directory ").Append(Quote(directory));
        }
        return sb.ToString();
    }

    // The value is pasted back into a shell, so anything with whitespace (usually a path) needs quoting.
    private static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}
