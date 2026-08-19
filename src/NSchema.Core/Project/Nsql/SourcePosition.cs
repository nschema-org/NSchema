namespace NSchema.Project.Nsql;

/// <summary>
/// A position in an NSchema source document.
/// <see cref="Line"/> and <see cref="Column"/> are 1-based (for error messages);
/// <see cref="Offset"/> is the 0-based character index (for raw slicing).
/// </summary>
/// <param name="Offset">The 0-based character offset into the source.</param>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Column">The 1-based column number.</param>
public readonly record struct SourcePosition(int Offset, int Line, int Column)
{
    /// <summary>
    /// The absent position: a node built synthetically rather than parsed.
    /// </summary>
    public static readonly SourcePosition None = default;

    /// <inheritdoc/>
    public override string ToString() => $"line {Line}, column {Column}";
}
