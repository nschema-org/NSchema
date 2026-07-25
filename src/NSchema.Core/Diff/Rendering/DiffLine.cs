using NSchema.Diff.Domain;

namespace NSchema.Diff.Rendering;

/// <summary>
/// A single line of a rendered <see cref="DatabaseDiff"/>.
/// </summary>
/// <param name="Kind">The change the line represents, or null for a blank spacer between blocks.</param>
/// <param name="Depth">The nesting level: 0 for an object header, 1 for a detail beneath it.</param>
/// <param name="Text">The rendered line content, without marker or indentation.</param>
public sealed record DiffLine(ChangeKind? Kind, int Depth, string Text)
{
    /// <summary>
    /// Represents a blank line in a diff document.
    /// </summary>
    public static readonly DiffLine Blank = new(null, 0, string.Empty);
};
