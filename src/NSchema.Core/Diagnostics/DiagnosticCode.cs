namespace NSchema.Diagnostics;

/// <summary>
/// What identifies a finding, independently of how it is worded.
/// </summary>
/// <remarks>
/// A code is a stable contract, not a description: it names a finding in configuration and documentation, so a
/// message can be reworded freely while the code stays put. Every code is unique across NSchema, because the
/// producer that reported a finding is not always known at compile time.
/// </remarks>
public readonly record struct DiagnosticCode
{
    /// <summary>
    /// The code with the given value.
    /// </summary>
    /// <param name="value">The code, in hyphen-separated lowercase words.</param>
    /// <exception cref="ArgumentException">The value is not a usable code.</exception>
    public DiagnosticCode(string value) =>
        Value = DiagnosticName.Validated(value, "code", "missing-dialect", nameof(value));

    /// <summary>
    /// The code as written.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// The code with the given value.
    /// </summary>
    /// <param name="value">The code, in hyphen-separated lowercase words.</param>
    public static implicit operator DiagnosticCode(string value) => new(value);
}
