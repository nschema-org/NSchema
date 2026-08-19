namespace NSchema.Diagnostics;

/// <summary>
/// What produced a finding.
/// </summary>
public readonly record struct DiagnosticSource
{
    /// <summary>
    /// The source with the given value.
    /// </summary>
    /// <param name="value">The producer's name, in hyphen-separated lowercase words.</param>
    /// <exception cref="ArgumentException">The value is not a usable source.</exception>
    public DiagnosticSource(string value) =>
        Value = DiagnosticName.Validated(value, "source", "sql-dialect", nameof(value));

    /// <summary>
    /// The source as written.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// The source with the given value.
    /// </summary>
    /// <param name="value">The producer's name, in hyphen-separated lowercase words.</param>
    public static implicit operator DiagnosticSource(string value) => new(value);
}
