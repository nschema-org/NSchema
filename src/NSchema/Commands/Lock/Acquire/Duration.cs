using System.Globalization;

namespace NSchema.Commands.Lock.Acquire;

/// <summary>
/// Parses the short human duration strings accepted by <c>lock acquire --ttl</c> — e.g. <c>30m</c>, <c>2h</c>,
/// <c>90s</c>, <c>1d</c> — falling back to the framework's <see cref="TimeSpan"/> format (e.g. <c>00:30:00</c>).
/// </summary>
internal static class Duration
{
    public static bool TryParse(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var unit = trimmed[^1];
        if (char.IsAsciiLetter(unit)
            && long.TryParse(trimmed[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            && amount > 0)
        {
            switch (char.ToLowerInvariant(unit))
            {
                case 's': value = TimeSpan.FromSeconds(amount); return true;
                case 'm': value = TimeSpan.FromMinutes(amount); return true;
                case 'h': value = TimeSpan.FromHours(amount); return true;
                case 'd': value = TimeSpan.FromDays(amount); return true;
                default: return false;
            }
        }

        return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out value) && value > TimeSpan.Zero;
    }

    public static TimeSpan Parse(string text) =>
        TryParse(text, out var value) ? value : throw new FormatException($"'{text}' is not a valid duration.");
}
