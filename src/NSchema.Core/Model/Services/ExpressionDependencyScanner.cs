namespace NSchema.Model.Services;

/// <summary>
/// Scans an opaque SQL expression for the call sites it references (<c>name(</c>, <c>schema.name(</c>),
/// in bare, bracket-quoted, or double-quoted spelling.
/// </summary>
/// <remarks>
/// Over-collecting is free — a reference only forms an ordering edge when it names an object in the same
/// database — so casts and builtins match harmlessly.
/// </remarks>
internal static class ExpressionDependencyScanner
{
    /// <summary>
    /// The call sites <paramref name="expression"/> references, unqualified names resolved against
    /// <paramref name="defaultSchema"/>.
    /// </summary>
    public static List<ObjectAddress> CallSites(string expression, SqlIdentifier defaultSchema)
    {
        var result = new List<ObjectAddress>();
        var seen = new HashSet<ObjectAddress>();

        // A three-token lookbehind is enough to see `name (` and `schema . name (`.
        var scanner = new SqlLexer(expression);
        var previous = new SqlToken[3];
        while (scanner.Next() is { Kind: not SqlTokenKind.End } token)
        {
            // A dollar-quoted block holds real SQL (a routine's whole body, typically): descend.
            if (token.Kind == SqlTokenKind.DollarString)
            {
                foreach (var address in CallSites(token.Value, defaultSchema))
                {
                    if (seen.Add(address))
                    {
                        result.Add(address);
                    }
                }
            }

            if (token.Kind == SqlTokenKind.LeftParen && IsName(previous[0]))
            {
                var address = previous is [_, { Kind: SqlTokenKind.Dot }, var schema] && IsName(schema)
                    ? new ObjectAddress(schema.Value, previous[0].Value)
                    : new ObjectAddress(defaultSchema, previous[0].Value);
                if (seen.Add(address))
                {
                    result.Add(address);
                }
            }

            previous[2] = previous[1];
            previous[1] = previous[0];
            previous[0] = token;
        }

        return result;
    }

    private static bool IsName(SqlToken token) => token.Kind is SqlTokenKind.Word or SqlTokenKind.QuotedIdentifier;
}
