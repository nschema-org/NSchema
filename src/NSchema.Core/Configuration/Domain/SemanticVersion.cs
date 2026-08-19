using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using NSchema.Model.Serialization;

namespace NSchema.Configuration.Domain;

/// <summary>
/// A semantic version: up to four numeric parts, an optional prerelease, and build metadata
/// (parsed and discarded — it never affects precedence).
/// </summary>
/// <remarks>
/// Precedence follows SemVer 2.0, with prerelease identifiers compared case-insensitively as NuGet compares
/// them; equality agrees with precedence.
/// </remarks>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
/// <param name="Patch">The patch version.</param>
/// <param name="Revision">The fourth numeric part, or zero when the version has only three.</param>
/// <param name="Prerelease">The prerelease identifiers, or <see langword="null"/> for a release version.</param>
[TypeConverter(typeof(ParsableTypeConverter<SemanticVersion>))]
[JsonConverter(typeof(ParsableJsonConverter<SemanticVersion>))]
public sealed record SemanticVersion(int Major, int Minor, int Patch, int Revision, string? Prerelease)
    : IComparable<SemanticVersion>, IParsable<SemanticVersion>
{
    /// <summary>
    /// Whether this is a prerelease version (it carries a prerelease label).
    /// </summary>
    public bool IsPrerelease => Prerelease is not null;

    /// <summary>
    /// Parses <paramref name="text"/> as a semantic version, throwing when it is not one.
    /// </summary>
    public static SemanticVersion Parse(string text, IFormatProvider? provider = null) => TryParse(text, out var version)
        ? version
        : throw new FormatException($"'{text}' is not a valid semantic version.");

    /// <inheritdoc cref="Parse(string, IFormatProvider?)" />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out SemanticVersion result)
    {
        result = s is not null && TryParse(s, out var version) ? version : null;
        return result is not null;
    }

    /// <summary>
    /// Parses <paramref name="text"/> as a semantic version.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        text = text.Trim();

        var metadata = text.IndexOf('+');
        if (metadata >= 0)
        {
            text = text[..metadata];
        }

        string? prerelease = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = text[(dash + 1)..];
            text = text[..dash];
            if (!IsValidPrerelease(prerelease))
            {
                return false;
            }
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }
        Span<int> numbers = stackalloc int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], numbers[3], prerelease);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var numeric = Major.CompareTo(other.Major);
        if (numeric == 0) { numeric = Minor.CompareTo(other.Minor); }
        if (numeric == 0) { numeric = Patch.CompareTo(other.Patch); }
        if (numeric == 0) { numeric = Revision.CompareTo(other.Revision); }
        if (numeric != 0)
        {
            return numeric;
        }

        // A prerelease precedes its release; two prereleases compare identifier by identifier.
        if (Prerelease is null || other.Prerelease is null)
        {
            return (Prerelease is null).CompareTo(other.Prerelease is null);
        }
        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    /// <inheritdoc />
    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Hash the way CompareTo compares: numeric prerelease identifiers by value, the rest case-insensitively.
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);
        hash.Add(Revision);
        foreach (var identifier in Prerelease?.Split('.') ?? [])
        {
            if (long.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                hash.Add(number);
            }
            else
            {
                hash.Add(identifier, StringComparer.OrdinalIgnoreCase);
            }
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}{(Revision > 0 ? $".{Revision}" : "")}{(Prerelease is null ? "" : $"-{Prerelease}")}";

    private static int ComparePrerelease(string left, string right)
    {
        var lefts = left.Split('.');
        var rights = right.Split('.');
        for (var i = 0; i < Math.Min(lefts.Length, rights.Length); i++)
        {
            var leftNumeric = long.TryParse(lefts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = long.TryParse(rights[i], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            var comparison = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1, // numeric identifiers precede alphanumeric ones
                (false, true) => 1,
                _ => string.Compare(lefts[i], rights[i], StringComparison.OrdinalIgnoreCase),
            };
            if (comparison != 0)
            {
                return comparison;
            }
        }
        return lefts.Length.CompareTo(rights.Length);
    }

    private static bool IsValidPrerelease(string prerelease) =>
        prerelease.Length > 0 && prerelease.Split('.').All(static identifier =>
            identifier.Length > 0 && identifier.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-'));
}
