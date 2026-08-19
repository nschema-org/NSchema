namespace NSchema.Model.Services;

/// <summary>
/// Extracts the objects a view reads from its (opaque) body, by scanning for the targets of <c>FROM</c> and
/// <c>JOIN</c> clauses. The scan is deliberately shallow — NSchema does not parse SQL — but it walks the whole
/// body, so references inside sub-queries are found too, and it subtracts names introduced by a <c>WITH</c>
/// common-table expression (those are local, not real objects).
/// </summary>
/// <remarks>
/// The extracted set only matters where a reference names an object that is <em>also</em> part of the same plan:
/// the planner forms an ordering edge only between two objects it is creating (or dropping) together. A spurious
/// reference (an alias, a function, a CTE the scan missed) therefore costs nothing — it matches no planned object
/// and produces no edge. The failure mode that matters is missing a genuine dependency, so the scan errs towards
/// over-collecting.
/// </remarks>
internal static class ViewDependencyExtractor
{
    // Keywords that end a table reference in a FROM list, so the scan stops treating following words as table
    // names (e.g. an alias is fine to over-collect, but these are never tables).
    private static readonly HashSet<string> _stops = new(StringComparer.OrdinalIgnoreCase)
    {
        "WHERE", "GROUP", "ORDER", "HAVING", "LIMIT", "OFFSET", "UNION", "INTERSECT", "EXCEPT",
        "ON", "USING", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "NATURAL",
        "WINDOW", "FETCH", "FOR", "AS",
    };

    /// <summary>
    /// Extracts the dependencies of a view from its body.
    /// </summary>
    /// <param name="body">The view's defining query (the text after <c>AS</c>).</param>
    /// <param name="defaultSchema">The schema an unqualified reference is resolved against (the view's own schema).</param>
    public static List<ObjectAddress> Extract(SqlText body, SqlIdentifier defaultSchema)
    {
        var tokens = Tokenize(body.Value);
        return new Scanner(tokens, defaultSchema, CollectCteNames(tokens)).Scan();
    }

    /// <summary>All significant tokens, via the shared lexical layer.</summary>
    private static List<SqlToken> Tokenize(string body)
    {
        var scanner = new SqlLexer(body);
        var tokens = new List<SqlToken>();
        while (scanner.Next() is { Kind: not SqlTokenKind.End } token)
        {
            tokens.Add(token);
        }
        return tokens;
    }

    /// <summary>A token that can name an object: a bare word or a quoted identifier.</summary>
    private static bool IsName(SqlToken token) => token.Kind is SqlTokenKind.Word or SqlTokenKind.QuotedIdentifier;

    /// <summary>
    /// One pass over a tokenized body, carrying the resolution context (default schema, CTE names) and the
    /// accumulating result so the per-clause readers don't thread them through every signature.
    /// </summary>
    private sealed class Scanner(IReadOnlyList<SqlToken> tokens, SqlIdentifier defaultSchema, HashSet<string> ctes)
    {
        private readonly List<ObjectAddress> _result = [];
        private readonly HashSet<ObjectAddress> _seen = [];

        public List<ObjectAddress> Scan()
        {
            // This outer scan visits every token, so FROM/JOIN clauses are found at any nesting depth (in
            // sub-queries, WHERE/SELECT-list scalar sub-queries, CTE bodies). The per-clause readers below use a
            // *local* cursor and never advance this loop, so descending into a sub-query to handle a comma list
            // can't hide the inner clauses from this scan.
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Kind == SqlTokenKind.DollarString)
                {
                    // A dollar-quoted block holds real SQL (a routine's body): its clauses count too.
                    foreach (var dependency in Extract(token.Value, defaultSchema))
                    {
                        Add(dependency);
                    }
                    continue;
                }
                if (token.Kind != SqlTokenKind.Word)
                {
                    continue;
                }

                if (Is(token, "FROM"))
                {
                    ReadFromList(i + 1);
                }
                else if (Is(token, "JOIN"))
                {
                    var cursor = i + 1;
                    TryReadReference(ref cursor);
                }
            }

            return _result;
        }

        /// <summary>Reads the comma-separated table references of a FROM clause, starting at <paramref name="j"/>.</summary>
        private void ReadFromList(int j)
        {
            while (j < tokens.Count)
            {
                if (!TryReadReference(ref j))
                {
                    break;
                }

                SkipAlias(ref j);
                if (j < tokens.Count && tokens[j].Kind == SqlTokenKind.Comma)
                {
                    j++;
                    continue;
                }
                break;
            }
        }

        /// <summary>
        /// Reads a single table reference at <paramref name="j"/>: a (optionally schema-qualified) name, or a
        /// parenthesised sub-query which is stepped over (its own FROM/JOIN clauses are found by the outer scan).
        /// Advances <paramref name="j"/> past what it read. Returns <see langword="false"/> when there is nothing
        /// to read (end of clause).
        /// </summary>
        private bool TryReadReference(ref int j)
        {
            if (j >= tokens.Count)
            {
                return false;
            }

            if (tokens[j].Kind == SqlTokenKind.LeftParen)
            {
                SkipBalancedParens(tokens, ref j);
                return true;
            }

            if (!IsName(tokens[j]) || (tokens[j].Kind == SqlTokenKind.Word && _stops.Contains(tokens[j].Value)))
            {
                return false;
            }

            var first = tokens[j].Value;
            j++;

            if (j + 1 < tokens.Count && tokens[j].Kind == SqlTokenKind.Dot && IsName(tokens[j + 1]))
            {
                var schema = first;
                var name = tokens[j + 1].Value;
                j += 2;
                Add(new ObjectAddress(schema, name));
                return true;
            }

            // Unqualified: a CTE name is local and must not be treated as a real object.
            if (!ctes.Contains(first))
            {
                Add(new ObjectAddress(defaultSchema, first));
            }
            return true;
        }

        /// <summary>
        /// Skips an optional table alias (<c>AS x</c> or a bare identifier) following a reference.
        /// </summary>
        private void SkipAlias(ref int j)
        {
            if (j >= tokens.Count || !IsName(tokens[j]))
            {
                return;
            }

            if (Is(tokens[j], "AS"))
            {
                j++;
                if (j < tokens.Count && IsName(tokens[j]))
                {
                    j++;
                }
                return;
            }

            if (tokens[j].Kind != SqlTokenKind.Word || !_stops.Contains(tokens[j].Value))
            {
                j++; // a bare alias, e.g. "users u"
            }
        }

        private void Add(ObjectAddress dependency)
        {
            if (_seen.Add(dependency))
            {
                _result.Add(dependency);
            }
        }
    }

    /// <summary>
    /// Collects the names introduced by common-table expressions: any <c>name AS (</c> binds a local name that
    /// must not be mistaken for a real object.
    /// </summary>
    private static HashSet<string> CollectCteNames(IReadOnlyList<SqlToken> tokens)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (IsName(tokens[i])
                && Is(tokens[i + 1], "AS")
                && tokens[i + 2].Kind == SqlTokenKind.LeftParen)
            {
                names.Add(tokens[i].Value);
            }
        }
        return names;
    }

    private static void SkipBalancedParens(IReadOnlyList<SqlToken> tokens, ref int i)
    {
        var depth = 0;
        for (; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == SqlTokenKind.LeftParen)
            {
                depth++;
            }
            else if (tokens[i].Kind == SqlTokenKind.RightParen)
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    return;
                }
            }
        }
    }

    private static bool Is(SqlToken token, string keyword) =>
        token.Kind == SqlTokenKind.Word && string.Equals(token.Value, keyword, StringComparison.OrdinalIgnoreCase);

}
