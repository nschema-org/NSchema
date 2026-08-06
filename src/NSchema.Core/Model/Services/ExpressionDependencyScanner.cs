using System.Text.RegularExpressions;

namespace NSchema.Model.Services;

/// <summary>
/// Scans an opaque SQL expression for the call sites it references (<c>name(</c>, <c>schema.name(</c>),
/// in bare, bracket-quoted, or double-quoted spelling.
/// </summary>
/// <remarks>
/// Over-collecting is free — a reference only forms an ordering edge when it names an object in the same
/// database — so casts and builtins match harmlessly.
/// </remarks>
internal static partial class ExpressionDependencyScanner
{
    /// <summary>
    /// The call sites <paramref name="expression"/> references, unqualified names resolved against
    /// <paramref name="defaultSchema"/>.
    /// </summary>
    public static List<ObjectAddress> CallSites(string expression, SqlIdentifier defaultSchema)
    {
        var result = new List<ObjectAddress>();
        var seen = new HashSet<ObjectAddress>();

        foreach (Match match in CallSite().Matches(expression))
        {
            var schema = match.Groups["schema"];
            var address = new ObjectAddress(
                schema.Success ? Unquote(schema.Value) : defaultSchema.Value,
                Unquote(match.Groups["name"].Value));
            if (seen.Add(address))
            {
                result.Add(address);
            }
        }

        return result;
    }

    private static string Unquote(string identifier) => identifier[0] switch
    {
        '[' => identifier[1..^1].Replace("]]", "]"),
        '"' => identifier[1..^1].Replace("\"\"", "\""),
        _ => identifier,
    };

    // A call site: name( or schema.name(, each part bare, [bracketed], or "quoted".
    [GeneratedRegex("""(?:(?<schema>[A-Za-z_][\w$]*|\[(?:[^\]]|\]\])+\]|"(?:[^"]|"")+")\s*\.\s*)?(?<name>[A-Za-z_][\w$]*|\[(?:[^\]]|\]\])+\]|"(?:[^"]|"")+")\s*\(""")]
    private static partial Regex CallSite();
}
