using System.Text;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

/// <summary>
/// Full-fidelity scanner for NSchema SQL.
/// Every character of the source is emitted, either as a token's raw text or as attached trivia.
/// </summary>
internal sealed class NsqlLexer(string source)
{
    private readonly string _source = source ?? throw new ArgumentNullException(nameof(source));

    private int _offset;
    private int _line = 1;
    private int _column = 1;

    private bool AtEnd => _offset >= _source.Length;
    private char Current => AtEnd ? '\0' : _source[_offset];
    private char Peek(int ahead) => _offset + ahead < _source.Length ? _source[_offset + ahead] : '\0';
    private SourcePosition Position => new(_offset, _line, _column);

    /// <summary>
    /// Returns the verbatim source between two offsets — the parser's hook for capturing opaque spans.
    /// </summary>
    public string Slice(int start, int end) => _source[start..end];

    /// <summary>
    /// Reads the next token, with its leading and trailing trivia attached.
    /// </summary>
    public Token Next()
    {
        var leading = ReadTrivia(trailing: false);

        var pos = Position;
        var start = _offset;
        var core = ScanCore(pos);
        var raw = _source[start.._offset];

        var trailing = ReadTrivia(trailing: true);

        return core with { Raw = raw, Leading = leading, Trailing = trailing };
    }

    /// <summary>
    /// Scans a single core token at the cursor (past any leading trivia). Doc-comments are core tokens; whitespace
    /// and non-doc comments have already been consumed as trivia by the caller.
    /// </summary>
    private Token ScanCore(SourcePosition pos)
    {
        if (AtEnd)
        {
            return new Token(TokenKind.EndOfFile, string.Empty, pos);
        }

        var ch = Current;

        // --- doc-comments (semantic, so tokens not trivia) ---
        if (ch == '-' && Peek(1) == '-' && Peek(2) == '-')
        {
            return ReadDocLine();
        }
        if (ch == '/' && Peek(1) == '*' && Peek(2) == '*' && Peek(3) != '/')
        {
            return ReadDocBlock();
        }

        // A dollar-quote ($$ … $$ or $tag$ … $tag$) is lexed whole; a bare '$' that opens no valid tag (e.g. $1)
        // falls through to the Symbol catch-all below.
        if (ch == '$' && PeekDollarTag() is { } tag)
        {
            return ReadDollarString(pos, tag);
        }

        // A '[' opens a bracket-quoted identifier unless the brackets are empty, in which case it falls through to
        // the Symbol catch-all below.
        if (ch == '[' && ReadBracketedIdentifier(pos) is { } bracketed)
        {
            return bracketed;
        }

        switch (ch)
        {
            case '(': Advance(); return new Token(TokenKind.LeftParen, NsqlSymbols.LeftParen, pos);
            case ')': Advance(); return new Token(TokenKind.RightParen, NsqlSymbols.RightParen, pos);
            case '{': Advance(); return new Token(TokenKind.LeftBrace, NsqlSymbols.LeftBrace, pos);
            case '}': Advance(); return new Token(TokenKind.RightBrace, NsqlSymbols.RightBrace, pos);
            case ',': Advance(); return new Token(TokenKind.Comma, NsqlSymbols.Comma, pos);
            case ';': Advance(); return new Token(TokenKind.Semicolon, NsqlSymbols.Semicolon, pos);
            case '.': Advance(); return new Token(TokenKind.Dot, NsqlSymbols.Dot, pos);
            case '=': Advance(); return new Token(TokenKind.Equals, NsqlSymbols.Equal, pos);
            case '-': Advance(); return new Token(TokenKind.Minus, NsqlSymbols.Minus, pos);
            case '\'': return ReadString(pos);
            case '"': return ReadQuotedIdentifier(pos);
        }

        if (char.IsAsciiDigit(ch))
        {
            return ReadInteger(pos);
        }

        if (IsIdentifierStart(ch))
        {
            return ReadIdentifier(pos);
        }

        // Any other character (an operator inside an opaque expression) is a single Symbol token. The lexer never
        // rejects it — the parser slices such expressions from the source rather than interpreting their tokens.
        Advance();
        return new Token(TokenKind.Symbol, ch.ToString(), pos);
    }

    /// <summary>
    /// Reads a run of trivia (whitespace, line breaks, and non-doc comments) at the cursor. Leading trivia runs
    /// to the next token; trailing trivia stops after the first line break (Roslyn's attachment rule), leaving the
    /// rest to the next token's leading.
    /// </summary>
    private List<Trivia> ReadTrivia(bool trailing)
    {
        List<Trivia>? list = null;
        while (ReadOneTrivia() is { } trivia)
        {
            (list ??= []).Add(trivia);
            if (trailing && trivia.Kind == TriviaKind.EndOfLine)
            {
                break;
            }
        }
        return list ?? [];
    }

    /// <summary>
    /// Reads a single trivia at the cursor, or returns <see langword="null"/> at a token start or end of input.
    /// </summary>
    private Trivia? ReadOneTrivia()
    {
        if (AtEnd)
        {
            return null;
        }

        var pos = Position;
        var c = Current;

        if (c is '\n' or '\r')
        {
            var start = _offset;
            Advance();
            if (c == '\r' && Current == '\n')
            {
                Advance();
            }
            return new Trivia(TriviaKind.EndOfLine, _source[start.._offset], pos);
        }

        if (c is ' ' or '\t')
        {
            var start = _offset;
            while (Current is ' ' or '\t')
            {
                Advance();
            }
            return new Trivia(TriviaKind.Whitespace, _source[start.._offset], pos);
        }

        // '---' is a doc-comment (a token); '--' is a source line comment (trivia).
        if (c == '-' && Peek(1) == '-' && Peek(2) != '-')
        {
            var start = _offset;
            while (!AtEnd && Current != '\n' && Current != '\r')
            {
                Advance();
            }
            return new Trivia(TriviaKind.LineComment, _source[start.._offset], pos);
        }

        // '/**…' is a doc-block (a token); '/* … */' (and empty '/**/') is a source block comment (trivia).
        if (c == '/' && Peek(1) == '*' && !(Peek(2) == '*' && Peek(3) != '/'))
        {
            var start = _offset;
            ReadBlockComment();
            return new Trivia(TriviaKind.BlockComment, _source[start.._offset], pos);
        }

        return null;
    }

    /// <summary>
    /// Returns the next token without consuming it.
    /// </summary>
    public Token Peek()
    {
        var (offset, line, column) = (_offset, _line, _column);
        var token = Next();
        (_offset, _line, _column) = (offset, line, column);
        return token;
    }

    /// <summary>
    /// Lexes a dollar-quoted string (<c>$$…$$</c> or <c>$tag$…$tag$</c>) starting at the current <c>$</c>, including
    /// both delimiters, as a single token whose text is the verbatim source. A differently-tagged quote inside is
    /// just content; only the opening tag closes the string.
    /// </summary>
    private Token ReadDollarString(SourcePosition pos, string tag)
    {
        var start = _offset;
        ConsumeLength(tag.Length); // opening tag
        while (!AtEnd)
        {
            if (Current == '$' && MatchesAt(tag))
            {
                ConsumeLength(tag.Length); // closing tag
                return new Token(TokenKind.DollarString, _source[start.._offset], pos);
            }
            Advance();
        }

        throw new NsqlSyntaxException("Unterminated dollar-quoted string", pos);
    }

    /// <summary>
    /// Returns the dollar-quote tag (<c>$$</c> or <c>$tag$</c>) opening at the current offset, or <see langword="null"/>
    /// when the <c>$</c> does not open a valid tag (Postgres rules: empty, or an identifier).
    /// </summary>
    private string? PeekDollarTag()
    {
        var i = 1;
        if (IsIdentifierStart(Peek(1)))
        {
            i++;
            while (IsIdentifierPart(Peek(i)))
            {
                i++;
            }
        }

        return Peek(i) == '$' ? _source.Substring(_offset, i + 1) : null;
    }

    /// <summary>Whether <paramref name="text"/> sits verbatim at the current offset.</summary>
    private bool MatchesAt(string text) =>
        _offset + text.Length <= _source.Length && string.CompareOrdinal(_source, _offset, text, 0, text.Length) == 0;

    private void ConsumeLength(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Advance();
        }
    }

    private Token ReadDocLine()
    {
        var pos = Position;
        var builder = new StringBuilder();
        while (true)
        {
            Advance(); Advance(); Advance(); // consume '---'
            var lineStart = _offset;
            while (!AtEnd && Current != '\n')
            {
                Advance();
            }
            builder.Append(_source[lineStart.._offset].Trim());

            if (!AtEnd)
            {
                Advance(); // consume the newline
            }

            // Merge an immediately-following doc-line (allowing leading indentation) into one comment.
            SkipInlineWhitespace();
            if (Current == '-' && Peek(1) == '-' && Peek(2) == '-')
            {
                builder.Append('\n');
                continue;
            }

            return new Token(TokenKind.DocComment, builder.ToString().Trim(), pos);
        }
    }

    private Token ReadDocBlock()
    {
        var pos = Position;
        Advance(); Advance(); Advance(); // consume '/**'
        var start = _offset;
        while (true)
        {
            if (AtEnd)
            {
                throw new NsqlSyntaxException("Unterminated doc-comment", pos);
            }
            if (Current == '*' && Peek(1) == '/')
            {
                var body = _source[start.._offset];
                Advance(); Advance(); // consume '*/'
                return new Token(TokenKind.DocComment, body.Trim(), pos);
            }
            Advance();
        }
    }

    /// <summary>
    /// Scans a delimited run at the cursor without consuming it, returning the decoded value and the raw length.
    /// A doubled closing delimiter escapes a literal one. <paramref name="what"/> names the run when it is unterminated.
    /// </summary>
    private (string Value, int Length) ScanDelimited(SourcePosition pos, char close, string what)
    {
        var builder = new StringBuilder();
        var ahead = 1; // past the opening delimiter
        while (true)
        {
            if (_offset + ahead >= _source.Length)
            {
                throw new NsqlSyntaxException($"Unterminated {what}", pos);
            }

            var c = Peek(ahead);
            if (c == close)
            {
                if (Peek(ahead + 1) == close)
                {
                    builder.Append(close);
                    ahead += 2;
                    continue;
                }
                return (builder.ToString(), ahead + 1);
            }

            builder.Append(c);
            ahead++;
        }
    }

    private Token ReadQuotedIdentifier(SourcePosition pos)
    {
        var (value, length) = ScanDelimited(pos, '"', "quoted identifier");
        if (value.Length == 0)
        {
            throw new NsqlSyntaxException("A quoted identifier cannot be empty", pos);
        }
        ConsumeLength(length);
        return new Token(TokenKind.QuotedIdentifier, value, pos);
    }

    /// <summary>
    /// Reads a bracketed identifier at the cursor, or returns <see langword="null"/> without consuming anything when
    /// the brackets hold no name.
    /// </summary>
    /// <remarks>
    /// An empty <c>[]</c> is left to the caller because it is how an array type is spelled, and one of those may sit
    /// inside an opaque expression the lexer must scan through.
    /// </remarks>
    private Token? ReadBracketedIdentifier(SourcePosition pos)
    {
        var (value, length) = ScanDelimited(pos, ']', "bracketed identifier");
        if (value.Length == 0)
        {
            return null;
        }
        ConsumeLength(length);
        return new Token(TokenKind.QuotedIdentifier, value, pos);
    }

    private Token ReadString(SourcePosition pos)
    {
        var (value, length) = ScanDelimited(pos, '\'', "string literal");
        ConsumeLength(length);
        return new Token(TokenKind.String, value, pos);
    }

    private Token ReadInteger(SourcePosition pos)
    {
        var start = _offset;
        while (!AtEnd && char.IsAsciiDigit(Current))
        {
            Advance();
        }
        return new Token(TokenKind.Integer, _source[start.._offset], pos);
    }

    private Token ReadIdentifier(SourcePosition pos)
    {
        var start = _offset;
        while (!AtEnd && IsIdentifierPart(Current))
        {
            Advance();
        }
        return new Token(TokenKind.Identifier, _source[start.._offset], pos);
    }

    private Token ReadBlockComment()
    {
        var pos = Position;
        var start = _offset;
        Advance(); Advance(); // consume '/*'
        while (true)
        {
            if (AtEnd)
            {
                throw new NsqlSyntaxException("Unterminated block comment", pos);
            }
            if (Current == '*' && Peek(1) == '/')
            {
                Advance(); Advance(); // consume '*/'
                return new Token(TokenKind.BlockComment, _source[start.._offset], pos);
            }
            Advance();
        }
    }

    private void SkipInlineWhitespace()
    {
        while (!AtEnd && Current is ' ' or '\t' or '\r')
        {
            Advance();
        }
    }

    private void Advance()
    {
        var c = _source[_offset++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    internal static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';
    internal static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
