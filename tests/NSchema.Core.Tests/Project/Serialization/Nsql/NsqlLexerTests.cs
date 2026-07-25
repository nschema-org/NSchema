using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class NsqlLexerTests
{
    private static List<Token> Tokens(string source)
    {
        var lexer = new NsqlLexer(source);
        var tokens = new List<Token>();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == TokenKind.EndOfFile)
            {
                return tokens;
            }
            tokens.Add(token);
        }
    }

    private static List<TokenKind> Kinds(string source) => Tokens(source).Select(t => t.Kind).ToList();

    // -------------------------------------------------------------------------
    // Punctuation, identifiers, literals
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_Punctuation_ProducesSymbolTokens()
    {
        Kinds("(){},;.=").ShouldBe(
        [
            TokenKind.LeftParen, TokenKind.RightParen, TokenKind.LeftBrace, TokenKind.RightBrace,
            TokenKind.Comma, TokenKind.Semicolon, TokenKind.Dot, TokenKind.Equals,
        ]);
    }

    [Fact]
    public void Lex_LoneMinus_IsAMinusToken()
    {
        // A lone '-' signs a numeric value (sequence options); '--' still starts a comment.
        Kinds("-1").ShouldBe([TokenKind.Minus, TokenKind.Integer]);
        Tokens("-- comment\n-").ShouldHaveSingleItem().Kind.ShouldBe(TokenKind.Minus);
    }

    [Fact]
    public void Lex_Identifier_PreservesCase()
    {
        var token = Tokens("Users_42").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.Identifier);
        token.Text.ShouldBe("Users_42");
    }

    [Fact]
    public void Lex_ObjectAddress_IsIdentifierDotIdentifier()
    {
        Tokens("app.users").Select(t => (t.Kind, t.Text)).ShouldBe(
            [(TokenKind.Identifier, "app"), (TokenKind.Dot, "."), (TokenKind.Identifier, "users")]);
    }

    [Theory]
    [InlineData("CREATE", "create", true)]
    [InlineData("create", "CREATE", true)]
    [InlineData("CREATE", "table", false)]
    public void IsKeyword_MatchesCaseInsensitively(string text, string keyword, bool expected)
        => Tokens(text).ShouldHaveSingleItem().IsKeyword(keyword).ShouldBe(expected);

    [Fact]
    public void IsKeyword_OnNonIdentifier_IsFalse()
        => Tokens("(").ShouldHaveSingleItem().IsKeyword("CREATE").ShouldBeFalse();

    [Fact]
    public void Lex_String_ReturnsUnescapedValueWithoutQuotes()
    {
        var token = Tokens("'hello world'").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.String);
        token.Text.ShouldBe("hello world");
    }

    [Fact]
    public void Lex_String_UnescapesDoubledQuotes()
        => Tokens("'it''s here'").ShouldHaveSingleItem().Text.ShouldBe("it's here");

    [Fact]
    public void Lex_UnterminatedString_Throws()
    {
        var ex = Should.Throw<NsqlSyntaxException>(() => Tokens("'oops"));
        ex.Message.ShouldContain("Unterminated string");
    }

    [Fact]
    public void Lex_Integer_ReturnsDigits()
    {
        var token = Tokens("1024").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.Integer);
        token.Text.ShouldBe("1024");
    }

    [Fact]
    public void Lex_OperatorCharacter_IsASymbolToken()
    {
        // Arrange
        // The lexer is context-free: operator characters that only appear inside opaque expressions are returned as
        // single Symbol tokens rather than rejected. The parser recovers such expressions by source-slicing.
        var tokens = Tokens("a > b");
        tokens.Select(t => t.Kind).ShouldBe([TokenKind.Identifier, TokenKind.Symbol, TokenKind.Identifier]);

        // Act
        var symbol = tokens[1];

        // Assert
        symbol.Text.ShouldBe(">");
        symbol.Position.Column.ShouldBe(3);
    }

    [Fact]
    public void Lex_MultiCharacterOperator_IsAdjacentSingleCharSymbols()
        => Tokens("a && b").Select(t => (t.Kind, t.Text)).ShouldBe(
        [
            (TokenKind.Identifier, "a"), (TokenKind.Symbol, "&"), (TokenKind.Symbol, "&"), (TokenKind.Identifier, "b"),
        ]);

    // -------------------------------------------------------------------------
    // Comments
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_SourceLineComment_IsSkipped()
        => Kinds("-- a note\nusers").ShouldBe([TokenKind.Identifier]);

    [Fact]
    public void Lex_SourceBlockComment_IsSkipped()
        => Kinds("a /* mid */ b").ShouldBe([TokenKind.Identifier, TokenKind.Identifier]);

    [Fact]
    public void Lex_EmptyBlockComment_IsSkippedNotTreatedAsDoc()
        => Kinds("a /**/ b").ShouldBe([TokenKind.Identifier, TokenKind.Identifier]);

    [Fact]
    public void Lex_DocLineComment_IsEmittedTrimmed()
    {
        // Act
        var token = Tokens("---   All users.   \nusers")[0];

        // Assert
        token.Kind.ShouldBe(TokenKind.DocComment);
        token.Text.ShouldBe("All users.");
    }

    [Fact]
    public void Lex_ConsecutiveDocLines_MergeIntoOneComment()
    {
        // Act
        var tokens = Tokens("--- Line one.\n--- Line two.\nusers");

        // Assert
        tokens[0].Kind.ShouldBe(TokenKind.DocComment);
        tokens[0].Text.ShouldBe("Line one.\nLine two.");
        tokens[1].Text.ShouldBe("users");
    }

    [Fact]
    public void Lex_DocBlockComment_IsEmittedTrimmed()
    {
        // Act
        var token = Tokens("/** Owning org. */ users")[0];

        // Assert
        token.Kind.ShouldBe(TokenKind.DocComment);
        token.Text.ShouldBe("Owning org.");
    }

    [Fact]
    public void Lex_DocLineThenDeclaration_AttachesNothingButOrdersBefore()
        => Kinds("--- doc\nCREATE").ShouldBe([TokenKind.DocComment, TokenKind.Identifier]);

    [Fact]
    public void Lex_UnterminatedBlockComment_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Tokens("/* never ends")).Message.ShouldContain("Unterminated block comment");

    [Fact]
    public void Lex_UnterminatedDocBlock_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Tokens("/** never ends")).Message.ShouldContain("Unterminated doc-comment");

    // -------------------------------------------------------------------------
    // Positions and lookahead
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_TracksLineAndColumnAcrossNewlines()
    {
        // Act
        var tokens = Tokens("CREATE\n  users");

        // Assert
        tokens[0].Position.ShouldBe(new SourcePosition(0, 1, 1));
        tokens[1].Position.Line.ShouldBe(2);
        tokens[1].Position.Column.ShouldBe(3);
    }

    [Fact]
    public void Peek_DoesNotConsume()
    {
        var lexer = new NsqlLexer("a b");
        lexer.Peek().Text.ShouldBe("a");
        lexer.Peek().Text.ShouldBe("a");   // still 'a'
        lexer.Next().Text.ShouldBe("a");
        lexer.Next().Text.ShouldBe("b");
    }

    [Fact]
    public void Next_AtEnd_ReturnsEndOfFileRepeatedly()
    {
        var lexer = new NsqlLexer("");
        lexer.Next().Kind.ShouldBe(TokenKind.EndOfFile);
        lexer.Next().Kind.ShouldBe(TokenKind.EndOfFile);
    }

    // -------------------------------------------------------------------------
    // Dollar-quoted strings — lexed whole, as a single DollarString token whose text keeps its delimiters, so an
    // internal ';' is never a structural terminator.
    // -------------------------------------------------------------------------

    [Fact]
    public void Lex_DollarString_IsOneTokenKeepingItsDelimiters()
    {
        var token = Tokens("$$ SELECT 1; $$").ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.DollarString);
        token.Text.ShouldBe("$$ SELECT 1; $$");
    }

    [Fact]
    public void Lex_DollarString_SwallowsInternalSemicolonsAndResumesAfterClosingTag()
        => Tokens("$$ SELECT 1; $$;").Select(t => (t.Kind, t.Text)).ShouldBe(
        [
            (TokenKind.DollarString, "$$ SELECT 1; $$"), (TokenKind.Semicolon, ";"),
        ]);

    [Fact]
    public void Lex_TaggedDollarString_ClosesOnlyOnItsOwnTag()
        // The inner $$ is just content; only the opening $body$ tag closes the string.
        => Tokens("$body$ SELECT '$$'; $$ ; $body$").ShouldHaveSingleItem().Text
            .ShouldBe("$body$ SELECT '$$'; $$ ; $body$");

    [Fact]
    public void Lex_NestedDifferentTags_OuterWins()
        => Tokens("$outer$ a $inner$ b; $inner$ c $outer$").ShouldHaveSingleItem().Text
            .ShouldBe("$outer$ a $inner$ b; $inner$ c $outer$");

    [Fact]
    public void Lex_DollarSignThatIsNotATag_IsASymbol()
    {
        // $1 is a parameter reference, not a dollar-quote tag, so the '$' is a plain Symbol token.
        Tokens("$1").Select(t => (t.Kind, t.Text)).ShouldBe(
            [(TokenKind.Symbol, "$"), (TokenKind.Integer, "1")]);
    }

    [Fact]
    public void Lex_UnterminatedDollarString_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Tokens("$$ SELECT 1;"))
            .Message.ShouldContain("Unterminated dollar-quoted string");
}
