using NSchema.Diff.Domain;

namespace NSchema.Diff.Rendering;

/// <summary>
/// A renderer-neutral, structured rendering of a <see cref="DatabaseDiff"/>.
/// </summary>
/// <param name="Lines">The body lines, in render order. Empty when the diff has no changes.</param>
/// <param name="Summary">The aggregate add/modify/remove counts, for the footer.</param>
public sealed record DiffDocument(IReadOnlyList<DiffLine> Lines, DiffSummary Summary)
{
    /// <summary>
    /// Renders <paramref name="diff"/> into a document shape a consumer can emit line by line.
    /// </summary>
    public static DiffDocument From(DatabaseDiff diff) => DiffRenderer.Render(diff);

    /// <summary>
    /// Whether the diff produced no changes (no body lines).
    /// </summary>
    public bool IsEmpty => Lines.Count == 0;
}
