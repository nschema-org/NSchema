namespace NSchema.Model.Services;

/// <summary>
/// A tolerant scanner over engine-native SQL.
/// </summary>
/// <remarks>
/// The alphabet is the permissive union of the engines NSchema targets. It covers nestable block comments,
/// <c>'…'</c> strings, <c>"…"</c> and <c>[…]</c> identifiers (each with doubling escapes), and
/// <c>$tag$ … $tag$</c> dollar quotes.
/// </remarks>
public sealed class SqlLexer(string text)
{
    private int _offset;
    private bool _endedInLineComment;

    /// <summary>
    /// Reads the next token, skipping whitespace and comments.
    /// </summary>
    public SqlToken Next()
    {
        SkipTrivia();
        if (_offset >= text.Length)
        {
            return new SqlToken(SqlTokenKind.End, "", text.Length, 0);
        }

        var start = _offset;
        var c = text[_offset];
        switch (c)
        {
            case '(': _offset++; return Punctuation(SqlTokenKind.LeftParen, start);
            case ')': _offset++; return Punctuation(SqlTokenKind.RightParen, start);
            case ',': _offset++; return Punctuation(SqlTokenKind.Comma, start);
            case '.': _offset++; return Punctuation(SqlTokenKind.Dot, start);
            case ';': _offset++; return Punctuation(SqlTokenKind.Semicolon, start);
            case '\'':
                _offset++; // opening quote
                ReadQuoted('\'');
                return new SqlToken(SqlTokenKind.String, text[start.._offset], start, _offset - start);
            case '"':
                return QuotedIdentifier('"', start);
            case '[':
                return QuotedIdentifier(']', start);
            case '$' when DollarTagEnd(start) is { } tagEnd:
                var inner = ReadDollarString(start, tagEnd);
                return new SqlToken(SqlTokenKind.DollarString, inner, start, _offset - start);
        }

        if (char.IsLetter(c) || c is '_' or '@' or '#')
        {
            while (_offset < text.Length && (char.IsLetterOrDigit(text[_offset]) || text[_offset] is '_' or '@' or '#' or '$'))
            {
                _offset++;
            }
            return new SqlToken(SqlTokenKind.Word, text[start.._offset], start, _offset - start);
        }

        if (char.IsDigit(c))
        {
            while (_offset < text.Length && (char.IsLetterOrDigit(text[_offset]) || text[_offset] == '.'))
            {
                _offset++;
            }
            return new SqlToken(SqlTokenKind.Other, text[start.._offset], start, _offset - start);
        }

        _offset++;
        return new SqlToken(SqlTokenKind.Other, text[start.._offset], start, 1);
    }

    /// <summary>
    /// The index of the first significant character: past leading whitespace and comments.
    /// </summary>
    public static int SkipLeadingTrivia(string sql)
    {
        var scanner = new SqlLexer(sql);
        scanner.SkipTrivia();
        return scanner._offset;
    }

    /// <summary>
    /// Whether the text ends inside a line comment — in which case anything printed after it on the same
    /// line is swallowed. A <c>--</c> inside a string or block comment does not count.
    /// </summary>
    public static bool EndsInLineComment(string sql)
    {
        var scanner = new SqlLexer(sql);
        while (scanner.Next().Kind != SqlTokenKind.End)
        {
        }
        return scanner._endedInLineComment;
    }

    /// <summary>
    /// Whether a <c>;</c> appears at parenthesis depth zero — the bare text would end a statement early.
    /// </summary>
    public static bool HasTopLevelSemicolon(string sql)
    {
        var scanner = new SqlLexer(sql);
        var depth = 0;
        while (true)
        {
            var token = scanner.Next();
            switch (token.Kind)
            {
                case SqlTokenKind.End:
                    return false;
                case SqlTokenKind.LeftParen:
                    depth++;
                    break;
                case SqlTokenKind.RightParen when depth > 0:
                    depth--;
                    break;
                case SqlTokenKind.Semicolon when depth == 0:
                    return true;
            }
        }
    }

    private static SqlToken Punctuation(SqlTokenKind kind, int start) => new(kind, "", start, 1);

    private SqlToken QuotedIdentifier(char close, int start)
    {
        _offset++; // opening quote
        var value = ReadQuoted(close);
        return new SqlToken(SqlTokenKind.QuotedIdentifier, value, start, _offset - start);
    }

    /// <summary>
    /// Reads to the closing character (a doubled closer is an escape), returning the inner text with the
    /// doubling undone. The cursor starts past the opener and ends past the closer (or at the end).
    /// </summary>
    private string ReadQuoted(char close)
    {
        var start = _offset;
        var doubled = false;
        while (_offset < text.Length)
        {
            if (text[_offset] == close)
            {
                if (_offset + 1 < text.Length && text[_offset + 1] == close)
                {
                    doubled = true;
                    _offset += 2;
                    continue;
                }
                var inner = text[start.._offset];
                _offset++;
                return doubled ? inner.Replace($"{close}{close}", $"{close}") : inner;
            }
            _offset++;
        }

        return text[start.._offset]; // unterminated: runs to the end
    }

    // The end of a dollar tag opening at `start` ($$ or $tag$), or null when the '$' opens no tag.
    private int? DollarTagEnd(int start)
    {
        var i = start + 1;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
        {
            i++;
        }
        return i < text.Length && text[i] == '$' ? i + 1 : null;
    }

    // Returns the inner text between the tags; the block's SQL is real SQL a reference scan may descend into.
    private string ReadDollarString(int start, int tagEnd)
    {
        var tag = text[start..tagEnd];
        var close = text.IndexOf(tag, tagEnd, StringComparison.Ordinal);
        _offset = close < 0 ? text.Length : close + tag.Length;
        return text[tagEnd..(close < 0 ? text.Length : close)];
    }

    private void SkipTrivia()
    {
        while (_offset < text.Length)
        {
            var c = text[_offset];
            if (char.IsWhiteSpace(c))
            {
                _offset++;
            }
            else if (c == '-' && _offset + 1 < text.Length && text[_offset + 1] == '-')
            {
                while (_offset < text.Length && text[_offset] != '\n')
                {
                    _offset++;
                }
                _endedInLineComment = _offset >= text.Length;
            }
            else if (c == '/' && _offset + 1 < text.Length && text[_offset + 1] == '*')
            {
                // Block comments nest, as the targeted engines' do.
                var nesting = 1;
                _offset += 2;
                while (_offset < text.Length && nesting > 0)
                {
                    if (text[_offset] == '/' && _offset + 1 < text.Length && text[_offset + 1] == '*')
                    {
                        nesting++;
                        _offset += 2;
                    }
                    else if (text[_offset] == '*' && _offset + 1 < text.Length && text[_offset + 1] == '/')
                    {
                        nesting--;
                        _offset += 2;
                    }
                    else
                    {
                        _offset++;
                    }
                }
            }
            else
            {
                return;
            }
        }
    }
}
