using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An interpolated-string handler for presenter messages.
/// </summary>
[InterpolatedStringHandler]
internal readonly ref struct ConsoleMessage(int literalLength, int formattedCount)
{
    // The one place the highlight style lives. Bold reads on any line colour (a green Success, a yellow Warn, …),
    // so emphasized holes never clash with the surrounding kind colour.
    private const string Highlight = "bold";

    private readonly StringBuilder _markup = new(literalLength + (formattedCount * 16));
    private readonly StringBuilder _text = new(literalLength + (formattedCount * 8));

    public void AppendLiteral(string value)
    {
        _text.Append(value);
        _markup.Append(Markup.Escape(value));
    }

    public void AppendFormatted<T>(T value) => AppendHole(value?.ToString() ?? string.Empty);

    public void AppendFormatted<T>(T value, string? format) =>
        AppendHole(value is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.CurrentCulture)
            : value?.ToString() ?? string.Empty);

    private void AppendHole(string value)
    {
        _text.Append(value);
        _markup.Append('[').Append(Highlight).Append(']').Append(Markup.Escape(value)).Append("[/]");
    }

    /// <summary>
    /// The Spectre markup: literals escaped, interpolated values wrapped in the highlight style.
    /// </summary>
    public string Styled => _markup.ToString();

    /// <summary>
    /// The plain text with no styling, for JSON output and logs.
    /// </summary>
    public string Plain => _text.ToString();
}
