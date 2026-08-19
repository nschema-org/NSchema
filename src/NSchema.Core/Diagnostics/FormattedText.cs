using System.Globalization;
using System.Runtime.CompilerServices;

namespace NSchema.Diagnostics;

/// <summary>
/// Renderer-neutral message text that remembers which spans were merged values.
/// </summary>
/// <remarks>
/// A hole that is prose rather than data (a display word, a pre-joined list) opts out with the <c>text</c> format, e.g. <c>{kind.Display():text}</c>.
/// </remarks>
[InterpolatedStringHandler]
#pragma warning disable CS9113 // Parameter is unread.
public sealed class FormattedText(int literalLength, int formattedCount) : IEquatable<FormattedText>
#pragma warning restore CS9113 // Parameter is unread.
{
    /// <summary>
    /// A single run of text: literal prose, or a merged value.
    /// </summary>
    /// <param name="Text">The run's text.</param>
    /// <param name="IsValue">Whether the run was a merged value rather than literal prose.</param>
    public readonly record struct Span(string Text, bool IsValue);

    private readonly List<Span> _spans = new(1 + formattedCount * 2);

    /// <summary>
    /// The spans in order. Adjacent runs of the same kind are merged, so the segmentation is canonical.
    /// </summary>
    public IReadOnlyList<Span> Spans => _spans;

    /// <summary>
    /// Appends literal prose. Called by the compiler.
    /// </summary>
    public void AppendLiteral(string value) => Append(value, isValue: false);

    /// <summary>
    /// Appends a merged value. Called by the compiler.
    /// </summary>
    public void AppendFormatted<T>(T value) => Append(value?.ToString() ?? string.Empty, isValue: true);

    /// <summary>
    /// Appends a formatted hole: the <c>text</c> format marks it as prose, any other format is applied
    /// through <see cref="IFormattable"/> and the result is a merged value. Called by the compiler.
    /// </summary>
    public void AppendFormatted<T>(T value, string? format)
    {
        if (format == "text")
        {
            Append(value?.ToString() ?? string.Empty, isValue: false);
            return;
        }

        Append(value is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.InvariantCulture)
            : value?.ToString() ?? string.Empty, isValue: true);
    }

    /// <summary>
    /// Splices another formatted text's spans intact. Called by the compiler.
    /// </summary>
    public void AppendFormatted(FormattedText value)
    {
        foreach (var span in value._spans)
        {
            Append(span.Text, span.IsValue);
        }
    }

    private void Append(string text, bool isValue)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_spans.Count > 0 && _spans[^1].IsValue == isValue)
        {
            _spans[^1] = new Span(_spans[^1].Text + text, isValue);
            return;
        }

        _spans.Add(new Span(text, isValue));
    }

    /// <summary>
    /// Wraps plain text as a single literal span.
    /// </summary>
    public static implicit operator FormattedText(string text)
    {
        var result = new FormattedText(text.Length, 0);
        result.AppendLiteral(text);
        return result;
    }

    /// <summary>
    /// The plain rendered text, with no span structure.
    /// </summary>
    public override string ToString() => string.Concat(_spans.Select(s => s.Text));

    /// <inheritdoc />
    public bool Equals(FormattedText? other) => other is not null && _spans.SequenceEqual(other._spans);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FormattedText);

    /// <inheritdoc />
    public override int GetHashCode() => string.GetHashCode(ToString(), StringComparison.Ordinal);
}
