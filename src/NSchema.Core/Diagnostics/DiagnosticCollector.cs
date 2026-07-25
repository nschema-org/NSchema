using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// Accumulates the diagnostics of a multi-step <see cref="Result"/> run.
/// </summary>
public sealed class DiagnosticCollector : DiagnosticCollection
{
    /// <summary>
    /// Absorbs a result's diagnostics; its value, if any, is left with the result.
    /// </summary>
    public void Add(Result result) => Add(result.Diagnostics);

    /// <summary>
    /// Absorbs a result's diagnostics and hands back its value; false when the result carried none.
    /// </summary>
    public bool TryTake<T>(Result<T> result, [NotNullWhen(true)] out T? value)
    {
        Add(result.Diagnostics);
        value = result.Value;
        return value is not null;
    }

    /// <summary>
    /// Absorbs a result's diagnostics and returns its value, which the caller asserts is present.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result carried no value.</exception>
    public T Require<T>(Result<T> result)
    {
        Add(result.Diagnostics);
        return result.Require();
    }

    /// <summary>
    /// Collapses to a value-less result carrying everything collected; a failure when any finding is an error.
    /// </summary>
    public Result ToResult() => Result.From(this);

    /// <summary>
    /// Collapses to a result carrying <paramref name="value"/> plus everything collected, the value riding
    /// whether or not the result is a failure.
    /// </summary>
    /// <typeparam name="T">The value the run produced.</typeparam>
    /// <param name="value">The produced value, when the run produced one.</param>
    public Result<T> ToResult<T>(T? value) => Result.From(value, this);
}
