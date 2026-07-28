using System.Reflection;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders the pieces both messengers present for an escaped exception: the message chain and the stack.
/// </summary>
/// <remarks>
/// Every expected failure — a broken configuration file, an unresolvable plugin, a policy violation — travels as
/// diagnostics on a <see cref="Result"/> and is rendered by the command that received it. So an exception reaching
/// the top level is, by construction, a defect in NSchema, and is reported as one.
/// </remarks>
internal static class ExceptionReport
{
    /// <summary>
    /// The URL an internal error points the reader at.
    /// </summary>
    public const string IssuesUrl = "https://github.com/nschema-org/NSchema/issues";

    /// <summary>
    /// The name of the exception type worth naming — the one left after the plumbing wrappers are stepped through.
    /// </summary>
    // Unwrapping a non-null exception always yields a non-null one; the compiler just can't see it through the switch.
    public static string TypeName(Exception exception) => Unwrap(exception)!.GetType().Name;

    /// <summary>
    /// The failure's message followed by each inner message that adds something, joined with <c>-&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The actionable detail routinely sits one or more levels down — a provider's connection failure arrives as
    /// <c>NpgsqlException -&gt; SocketException("Connection refused")</c> — so reporting only the outermost message
    /// throws away the part the reader needed. Messages already quoted by an outer one are dropped, since wrapping
    /// exceptions habitually repeat their inner message verbatim.
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

    /// <summary>
    /// The stack to print for an internal error: the full <see cref="Exception.ToString"/> (so inner exceptions keep
    /// their own frames) minus the frames the reader can do nothing with.
    /// </summary>
    public static string Stack(Exception exception) => string.Join(Environment.NewLine,
        exception.ToString()
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Where(line => !IsNoise(line)));

    // Async plumbing and the System.CommandLine invocation path sit between Program and the command handler on every
    // single trace. Dropping them keeps the frames that name NSchema code — the ones that locate the bug — near the
    // top, where someone skimming a pasted issue will actually read them.
    private static bool IsNoise(string line)
    {
        var frame = line.AsSpan().TrimStart();

        return frame.StartsWith("at System.Runtime.CompilerServices.", StringComparison.Ordinal)
            || frame.StartsWith("at System.Runtime.ExceptionServices.", StringComparison.Ordinal)
            || frame.StartsWith("at System.Threading.Tasks.", StringComparison.Ordinal)
            || frame.StartsWith("at System.CommandLine.", StringComparison.Ordinal)
            || frame.StartsWith("--- End of stack trace from previous location ---", StringComparison.Ordinal);
    }

    // AggregateException and TargetInvocationException are carriers: their own messages say nothing about what failed,
    // and the plugin loader's reflection boundary makes both routine. Step through to the exception that matters. An
    // aggregate holding several failures is kept, because its message is the only thing listing all of them.
    private static Exception? Unwrap(Exception? exception) => exception switch
    {
        AggregateException { InnerExceptions.Count: 1 } aggregate => Unwrap(aggregate.InnerExceptions[0]),
        TargetInvocationException { InnerException: { } inner } => Unwrap(inner),
        _ => exception,
    };
}
