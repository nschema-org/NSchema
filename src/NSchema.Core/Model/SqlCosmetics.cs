using System.Text;

namespace NSchema.Model;

/// <summary>
/// Normalizes the cosmetic differences of a SQL fragment — insignificant whitespace and a trailing
/// terminator — literal-aware, so quoted runs are preserved verbatim.
/// </summary>
internal static class SqlCosmetics
{
    internal static string Normalize(string body)
    {
        var sb = new StringBuilder(body.Length);
        var pendingSpace = false;

        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];

            if (c is '\'' or '"')
            {
                // A quoted run is significant verbatim. Emit any pending separator, then copy the whole
                // literal (including doubled-quote escapes like '' / "") without touching its contents.
                if (pendingSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                }
                pendingSpace = false;
                i = CopyQuotedRun(body, i, sb);
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true; // collapse any run of whitespace to a single separator, emitted lazily
                continue;
            }

            if (pendingSpace && sb.Length > 0)
            {
                sb.Append(' ');
            }
            pendingSpace = false;
            sb.Append(c);
        }

        // A trailing terminator is cosmetic, as is any separator space we emitted just before it (e.g.
        // "... users ;"). Any interior ';' is left intact, and a body ending in a literal ends with a quote
        // (never ';'), so this can't reach inside a string.
        while (sb.Length > 0 && (sb[^1] == ';' || sb[^1] == ' '))
        {
            sb.Length--;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Copies the quoted run starting at <paramref name="start"/> (the opening quote) verbatim into
    /// <paramref name="sb"/>, honouring doubled-quote escapes, and returns the index of the closing quote.
    /// </summary>
    private static int CopyQuotedRun(string body, int start, StringBuilder sb)
    {
        var quote = body[start];
        sb.Append(quote);

        for (var i = start + 1; i < body.Length; i++)
        {
            var c = body[i];
            sb.Append(c);
            if (c != quote)
            {
                continue;
            }

            // A doubled quote is an escaped quote, not the close — consume the pair and stay inside.
            if (i + 1 < body.Length && body[i + 1] == quote)
            {
                sb.Append(quote);
                i++;
                continue;
            }

            return i; // closing quote
        }

        return body.Length - 1; // unterminated literal: copied to the end
    }
}
