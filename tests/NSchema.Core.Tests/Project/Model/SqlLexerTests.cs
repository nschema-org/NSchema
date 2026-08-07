using NSchema.Model.Services;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The shared lexical layer under every opaque-SQL helper: tolerant, permissive-union alphabet, never
/// throws. These pin the alphabet decisions each consumer used to make privately.
/// </summary>
public sealed class SqlLexerTests
{
    private static List<SqlToken> Tokens(string sql)
    {
        var scanner = new SqlLexer(sql);
        var tokens = new List<SqlToken>();
        while (scanner.Next() is { Kind: not SqlTokenKind.End } token)
        {
            tokens.Add(token);
        }
        return tokens;
    }

    [Fact]
    public void Next_QuotingStyles_UnquoteWithDoublingUndone()
    {
        var tokens = Tokens("""[Order ]]Details]] x] "we""ird" bare""");
        tokens.Select(t => (t.Kind, t.Value)).ShouldBe([
            (SqlTokenKind.QuotedIdentifier, "Order ]Details] x"),
            (SqlTokenKind.QuotedIdentifier, "we\"ird"),
            (SqlTokenKind.Word, "bare"),
        ]);
    }

    [Fact]
    public void Next_BracketedKeywordSpelling_IsNotAWord()
    {
        // [AS] names an object; only a bare word may match a keyword check.
        Tokens("[AS]").ShouldHaveSingleItem().Kind.ShouldBe(SqlTokenKind.QuotedIdentifier);
    }

    [Fact]
    public void Next_NestedBlockComments_AreOneTrivia()
    {
        Tokens("a /* outer /* inner */ still outer */ b")
            .Select(t => t.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Next_StringsAndDollarQuotes_SwallowTheirContents()
    {
        var tokens = Tokens("before 'a; (b' $tag$ ; ( -- not trivia $tag$ after");
        tokens.Select(t => t.Kind).ShouldBe([
            SqlTokenKind.Word, SqlTokenKind.String, SqlTokenKind.DollarString, SqlTokenKind.Word,
        ]);
    }

    [Fact]
    public void Next_UnterminatedConstructs_RunToTheEnd_WithoutThrowing()
    {
        Tokens("'never closed").ShouldHaveSingleItem().Kind.ShouldBe(SqlTokenKind.String);
        Tokens("$$ never closed").ShouldHaveSingleItem().Kind.ShouldBe(SqlTokenKind.DollarString);
        Tokens("[never closed").ShouldHaveSingleItem().Kind.ShouldBe(SqlTokenKind.QuotedIdentifier);
    }

    [Fact]
    public void Next_TokensCarryTheirSourceOffsets()
    {
        var token = Tokens("  hello").ShouldHaveSingleItem();
        token.Start.ShouldBe(2);
        token.Length.ShouldBe(5);
    }

    [Fact]
    public void SkipLeadingTrivia_StepsOverCommentsAndWhitespace()
    {
        var sql = "  -- banner\n/* block /* nested */ */  CREATE";
        sql[SqlLexer.SkipLeadingTrivia(sql)..].ShouldBe("CREATE");
    }

    [Fact]
    public void EndsInLineComment_OnlyForARealTrailingComment()
    {
        SqlLexer.EndsInLineComment("x -- trailing").ShouldBeTrue();
        SqlLexer.EndsInLineComment("x -- inner\ny").ShouldBeFalse();
        SqlLexer.EndsInLineComment("x = '--not a comment'").ShouldBeFalse();
        SqlLexer.EndsInLineComment("x /* -- inside block */").ShouldBeFalse();
    }

    [Fact]
    public void HasTopLevelSemicolon_IgnoresNestedAndQuotedOnes()
    {
        SqlLexer.HasTopLevelSemicolon("BEGIN RETURN 1; END").ShouldBeTrue();
        SqlLexer.HasTopLevelSemicolon("f(a; b)").ShouldBeFalse();
        SqlLexer.HasTopLevelSemicolon("'a;b'").ShouldBeFalse();
        SqlLexer.HasTopLevelSemicolon("$$ a; b $$").ShouldBeFalse();
    }
}
