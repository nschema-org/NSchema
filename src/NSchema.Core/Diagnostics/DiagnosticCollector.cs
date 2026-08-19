using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// Accumulates the diagnostics of a multi-step <see cref="Result"/> run.
/// </summary>
/// <typeparam name="TDiagnostic">The diagnostic type the run's steps mint.</typeparam>
public class DiagnosticCollector<TDiagnostic> : DiagnosticCollection<TDiagnostic> where TDiagnostic : Diagnostic
{
    /// <summary>
    /// Absorbs a result's diagnostics; its value, if any, is left with the result.
    /// </summary>
    public void Add<T>(Result<T, TDiagnostic> result) => AddRange(result.Diagnostics);

    /// <summary>
    /// Absorbs a result's diagnostics and hands back its value; false when the result carried none.
    /// </summary>
    public bool TryTake<T>(Result<T, TDiagnostic> result, [NotNullWhen(true)] out T? value)
    {
        AddRange(result.Diagnostics);
        value = result.Value;
        return value is not null;
    }

    /// <summary>
    /// Absorbs a result's diagnostics and returns its value, which the caller asserts is present.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result carried no value.</exception>
    public T Require<T>(Result<T, TDiagnostic> result)
    {
        AddRange(result.Diagnostics);
        return result.Require();
    }
}

/// <summary>
/// A <see cref="DiagnosticCollector{TDiagnostic}"/> over the base <see cref="Diagnostic"/>.
/// </summary>
public sealed class DiagnosticCollector : DiagnosticCollector<Diagnostic>
{
    /// <summary>
    /// Absorbs a result's diagnostics; its value, if any, is left with the result.
    /// </summary>
    public void AddRange(Result result) => AddRange(result.Diagnostics);

    /// <summary>
    /// Absorbs a result's diagnostics and hands back its value; false when the result carried none.
    /// </summary>
    public bool TryTake<T>(Result<T> result, [NotNullWhen(true)] out T? value)
    {
        AddRange(result);
        value = result.Value;
        return value is not null;
    }

    /// <summary>
    /// Absorbs a result's diagnostics and returns its value, which the caller asserts is present.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result carried no value.</exception>
    public T Require<T>(Result<T> result)
    {
        AddRange(result);
        return result.Require();
    }
}
