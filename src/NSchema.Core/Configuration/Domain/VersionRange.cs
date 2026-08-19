using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model.Serialization;

namespace NSchema.Configuration.Domain;

/// <summary>
/// A version range in interval notation as NuGet writes it (<c>[5.0,6.0)</c>, <c>(,6.0)</c>, <c>[5.0.1]</c>),
/// with one deliberate divergence — a bare version means exactly that version, not a minimum.
/// </summary>
/// <remarks>
/// Floating versions (<c>5.0.*</c>) are not part of the grammar: a float is a resolution instruction, not a
/// constraint, and resolution already picks the highest version a range admits.
/// A range admits only release versions unless one of its bounds is a prerelease — you opt into prereleases
/// by naming one (an exact pin, or a prerelease bound).
/// Equality is structural (<c>[5.0,6.0)</c> equals <c>[5.0.0, 6.0.0)</c>), and <see cref="ToString"/> renders the canonical text,
/// which feeds package resolution.
/// </remarks>
[TypeConverter(typeof(ParsableTypeConverter<VersionRange>))]
[JsonConverter(typeof(ParsableJsonConverter<VersionRange>))]
public sealed record VersionRange : IParsable<VersionRange>
{
    private VersionRange(SemanticVersion? minimum, bool minimumInclusive, SemanticVersion? maximum, bool maximumInclusive)
    {
        Minimum = minimum;
        MinimumInclusive = minimumInclusive;
        Maximum = maximum;
        MaximumInclusive = maximumInclusive;
    }

    private SemanticVersion? Minimum { get; }
    private bool MinimumInclusive { get; }
    private SemanticVersion? Maximum { get; }
    private bool MaximumInclusive { get; }

    /// <summary>
    /// Whether the range admits exactly one version — an exact pin (<c>[5.0.1]</c>), which is its own resolution.
    /// </summary>
    public bool IsExact => Minimum is not null && Minimum == Maximum && MinimumInclusive && MaximumInclusive;

    /// <summary>
    /// The single version an exact pin admits, or <see langword="null"/> when the range admits more than one.
    /// </summary>
    public SemanticVersion? ExactVersion => IsExact ? Minimum : null;

    /// <summary>
    /// Parses <paramref name="text"/> under the range grammar, throwing when it is neither a version nor a
    /// version range.
    /// </summary>
    public static VersionRange Parse(string text, IFormatProvider? provider = null) => TryParse(text, out var range)
        ? range
        : throw new FormatException($"'{text}' is not a valid version or version range.");

    /// <inheritdoc cref="Parse(string, IFormatProvider?)" />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out VersionRange result)
    {
        result = s is not null && TryParse(s, out var range) ? range : null;
        return result is not null;
    }

    /// <summary>
    /// Parses <paramref name="text"/> under the range grammar: a bare version (exact), or an interval.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out VersionRange? range)
    {
        range = null;
        text = text.Trim();
        if (SemanticVersion.TryParse(text, out var exact))
        {
            range = new VersionRange(exact, true, exact, true);
            return true;
        }

        if (text.Length < 3)
        {
            return false;
        }
        var minimumInclusive = text[0] == '[';
        var maximumInclusive = text[^1] == ']';
        if ((!minimumInclusive && text[0] != '(') || (!maximumInclusive && text[^1] != ')'))
        {
            return false;
        }

        var inner = text[1..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0)
        {
            // No comma: the exact form, which must be fully inclusive — [5.0.1].
            if (!minimumInclusive || !maximumInclusive || !SemanticVersion.TryParse(inner, out var single))
            {
                return false;
            }
            range = new VersionRange(single, true, single, true);
            return true;
        }
        if (inner.IndexOf(',', comma + 1) >= 0)
        {
            return false;
        }

        SemanticVersion? minimum = null;
        SemanticVersion? maximum = null;
        var minimumText = inner[..comma].Trim();
        var maximumText = inner[(comma + 1)..].Trim();
        // An unbounded end must be exclusive: '(,6.0)' — never '[,6.0)'.
        if (minimumText.Length > 0 ? !SemanticVersion.TryParse(minimumText, out minimum) : minimumInclusive)
        {
            return false;
        }
        if (maximumText.Length > 0 ? !SemanticVersion.TryParse(maximumText, out maximum) : maximumInclusive)
        {
            return false;
        }
        if (minimum is null && maximum is null)
        {
            return false;
        }

        range = new VersionRange(minimum, minimumInclusive, maximum, maximumInclusive);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="version"/> falls inside the range. A range admits a prerelease only when one of
    /// its bounds is itself a prerelease — otherwise prereleases are opted into explicitly, by naming one.
    /// </summary>
    public bool Satisfies(SemanticVersion version)
    {
        if (version.IsPrerelease && Minimum?.IsPrerelease is not true && Maximum?.IsPrerelease is not true)
        {
            return false;
        }

        if (Minimum is not null)
        {
            var comparison = version.CompareTo(Minimum);
            if (MinimumInclusive ? comparison < 0 : comparison <= 0)
            {
                return false;
            }
        }
        if (Maximum is not null)
        {
            var comparison = version.CompareTo(Maximum);
            if (MaximumInclusive ? comparison > 0 : comparison >= 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The highest of <paramref name="available"/> that falls inside the range, or <see langword="null"/> when
    /// none do. Selecting from a supplied set keeps the choice pure — the caller enumerates what a feed offers.
    /// </summary>
    public SemanticVersion? Highest(IEnumerable<SemanticVersion> available) => available.Where(Satisfies).Max();

    /// <inheritdoc />
    public override string ToString() => IsExact
        ? $"[{Minimum}]"
        : $"{(MinimumInclusive ? '[' : '(')}{Minimum},{Maximum}{(MaximumInclusive ? ']' : ')')}";
}
