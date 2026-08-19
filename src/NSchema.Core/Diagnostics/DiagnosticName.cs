using System.Text.RegularExpressions;

namespace NSchema.Diagnostics;

/// <summary>
/// The shape a diagnostic's source and code share.
/// </summary>
internal static partial class DiagnosticName
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Shape { get; }

    /// <summary>
    /// Returns <paramref name="value"/> when it is a usable name, and throws when it is not.
    /// </summary>
    /// <param name="value">The candidate name.</param>
    /// <param name="what">What the name names, for the failure message.</param>
    /// <param name="example">An example of a usable name.</param>
    /// <param name="paramName">The parameter being validated.</param>
    /// <exception cref="ArgumentException">The value is not a usable name.</exception>
    public static string Validated(string value, string what, string example, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!Shape.IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a diagnostic {what}. A {what} is written in hyphen-separated lowercase words "
                + $"(e.g. '{example}'), so that it is usable as a settings key.", paramName);
        }

        return value;
    }
}
