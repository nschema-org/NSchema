using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// The outcome of an operation that yields no value.
/// </summary>
public class Result
{
    internal Result(IDiagnosticCollection<Diagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Whether the operation succeeded; true if there are no errors.
    /// </summary>
    public virtual bool IsSuccess => !Diagnostics.HasErrors;

    /// <summary>
    /// Whether the operation failed; true if there are errors.
    /// </summary>
    public virtual bool IsFailure => Diagnostics.HasErrors;

    /// <summary>
    /// Every finding produced, of any severity.
    /// </summary>
    public IDiagnosticCollection<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Errors;

    /// <summary>
    /// The warning-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Warnings => Diagnostics.Warnings;

    /// <summary>
    /// A clean value-less success.
    /// </summary>
    public static Result Success() => new(new DiagnosticCollection());

    /// <summary>
    /// A value-less result built from an aggregated set of diagnostics; a failure when any is an error.
    /// </summary>
    /// <param name="diagnostics">Every finding produced.</param>
    public static Result From(params IEnumerable<Diagnostic> diagnostics) => new(new DiagnosticCollection(diagnostics));

    /// <summary>
    /// A successful <see cref="Result{T}"/> carrying <paramref name="value"/>, optionally with advisory diagnostics.
    /// </summary>
    /// <typeparam name="T">The value produced on success.</typeparam>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    public static Result<T> Success<T>(T value, params IEnumerable<Diagnostic> diagnostics) => new(value, new DiagnosticCollection(diagnostics));

    /// <summary>
    /// A failed <see cref="Result{T}"/> — no value — carrying the error diagnostics that explain it.
    /// </summary>
    /// <typeparam name="T">The value the result would have produced on success.</typeparam>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    public static Result<T> Failure<T>(params IEnumerable<Diagnostic> diagnostics) => new(default, new DiagnosticCollection(diagnostics));

    /// <summary>
    /// A <see cref="Result{T}"/> built from <paramref name="value"/> plus an aggregated set of diagnostics, carrying the
    /// value whether or not the result is a failure.
    /// </summary>
    /// <typeparam name="T">The value produced.</typeparam>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Every finding produced, if any.</param>
    public static Result<T> From<T>(T? value, IEnumerable<Diagnostic> diagnostics) => new(value, new DiagnosticCollection(diagnostics));
}

/// <summary>
/// The outcome of an operation that yields a <typeparamref name="T"/> on success or fails with reasons.
/// </summary>
/// <typeparam name="T">The value produced on success.</typeparam>
public class Result<T> : Result
{
    internal Result(T? value, IDiagnosticCollection<Diagnostic> diagnostics) : base(diagnostics)
    {
        Value = value;
    }

    /// <summary>
    /// The produced value; non-null on success, and sometimes on a failure when the operation produced data despite finding errors.
    /// </summary>
    public T? Value { get; }

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool IsSuccess => base.IsSuccess;

    /// <inheritdoc />
    [MemberNotNullWhen(false, nameof(Value))]
    public override bool IsFailure => base.IsFailure;

    /// <summary>
    /// Returns the value of the result, or throws an exception if it's missing.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result carried no value.</exception>
    public T Require() => Value ?? throw new InvalidOperationException($"The result was required to carry a value but did not. Diagnostics: {string.Join("; ", Diagnostics.Select(d => d.Message))}");

    /// <summary>
    /// Projects the carried value through <paramref name="map"/>, propagating the diagnostics.
    /// </summary>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="map">The projection applied to <see cref="Value"/> when present.</param>
    /// <returns>The mapped result.</returns>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) => new(Value != null ? map(Value) : default, Diagnostics);

    /// <summary>
    /// Lifts a value into a successful result, so a method can <c>return value;</c> directly.
    /// </summary>
    /// <param name="value">The value to lift.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Lifts a single diagnostic into a result, so a method can return one directly.
    /// (a failure when the diagnostic is an error).
    /// </summary>
    /// <param name="diagnostic">The diagnostic to carry.</param>
    public static implicit operator Result<T>(Diagnostic diagnostic) => Failure<T>(diagnostic);
}

/// <summary>
/// A <see cref="Result{T}"/> whose diagnostics are a specialized type — a seam whose findings carry
/// structured context (a source position, an offending node) exposes them typed, and the result still
/// folds upward as a plain <see cref="Result{T}"/> without translation.
/// </summary>
/// <typeparam name="TValue">The value produced on success.</typeparam>
/// <typeparam name="TDiagnostic">The diagnostic type the producer mints.</typeparam>
public sealed class Result<TValue, TDiagnostic> : Result<TValue> where TDiagnostic : Diagnostic
{
    internal Result(TValue? value, IDiagnosticCollection<TDiagnostic> diagnostics) : base(value, diagnostics)
    {
    }

    /// <summary>
    /// Every finding produced, of any severity, in the producer's own diagnostic type.
    /// </summary>
    public new IDiagnosticCollection<TDiagnostic> Diagnostics => (IDiagnosticCollection<TDiagnostic>)base.Diagnostics;

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public new IEnumerable<TDiagnostic> Errors => Diagnostics.Errors;

    /// <summary>
    /// The warning-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public new IEnumerable<TDiagnostic> Warnings => Diagnostics.Warnings;

    /// <summary>
    /// A successful result carrying <paramref name="value"/>, optionally with advisory diagnostics.
    /// </summary>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    public static Result<TValue, TDiagnostic> Success(TValue value, params IEnumerable<TDiagnostic> diagnostics) => new(value, new DiagnosticCollection<TDiagnostic>(diagnostics));

    /// <summary>
    /// A failed result — no value — carrying the diagnostics that explain it.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    public static Result<TValue, TDiagnostic> Failure(params IEnumerable<TDiagnostic> diagnostics) => new(default, new DiagnosticCollection<TDiagnostic>(diagnostics));

    /// <summary>
    /// A result built from <paramref name="value"/> plus an aggregated set of diagnostics, carrying the value
    /// whether or not the result is a failure.
    /// </summary>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Every finding produced.</param>
    public static Result<TValue, TDiagnostic> From(TValue? value, IEnumerable<TDiagnostic> diagnostics) => new(value, new DiagnosticCollection<TDiagnostic>(diagnostics));
}
