namespace NSchema.Diagnostics;

/// <summary>
/// Describes a thrown exception for a diagnostic message.
/// </summary>
/// <remarks>
/// Public because a plugin needs it: a backend catches what it anticipates and turns it into a failure diagnostic, and
/// this composes the message for it.
/// </remarks>
public static class ExceptionMessage
{
    /// <summary>
    /// The exception's message followed by each inner message that adds something, joined with <c>-&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The actionable detail usually sits one level down — a provider's connection failure arrives as
    /// <c>NpgsqlException -&gt; SocketException("Connection refused")</c> — so the outermost message alone throws
    /// away the part the reader needed. A message an outer one already quotes is dropped.
    /// </remarks>
    public static string Describe(Exception exception)
    {
        var messages = new List<string>();

        for (var current = Unwrap(exception); current is not null; current = Unwrap(current.InnerException))
        {
            if (!messages.Any(seen => seen.Contains(current.Message, StringComparison.Ordinal)))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" -> ", messages);
    }

    // An aggregate carrying one failure says nothing its inner exception doesn't; one carrying several is kept,
    // because its own message is the only thing listing them all.
    private static Exception? Unwrap(Exception? exception)
    {
        while (true)
        {
            if (exception is AggregateException { InnerExceptions.Count: 1 } aggregate)
            {
                exception = aggregate.InnerExceptions[0];
                continue;
            }

            return exception;
        }
    }
}
