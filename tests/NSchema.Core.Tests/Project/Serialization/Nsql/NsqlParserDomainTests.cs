using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for <c>CREATE DOMAIN s.d AS &lt;type&gt; [NOT NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr]</c>.
/// </summary>
public sealed class NsqlParserDomainTests
{
    private static DomainType ParseDomain(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; " + sql).Parse().Database
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleDomain_CapturesNameAndType()
    {
        // Act
        var domain = ParseDomain("CREATE DOMAIN app.typeid AS text;");

        // Assert
        domain.Name.ShouldBe("typeid");
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeFalse();
        domain.Default.ShouldBeNull();
        domain.Checks.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NotNull_SetsFlag()
        => ParseDomain("CREATE DOMAIN app.d AS text NOT NULL;").NotNull.ShouldBeTrue();

    [Fact]
    public void Parse_Default_IsCapturedVerbatim()
        => ParseDomain("CREATE DOMAIN app.d AS text DEFAULT 'n/a';").Default.ShouldBe("'n/a'");

    [Fact]
    public void Parse_Check_IsCaptured()
    {
        var check = ParseDomain("CREATE DOMAIN app.d AS text CONSTRAINT d_chk CHECK (VALUE <> '');").Checks.ShouldHaveSingleItem();
        check.Name.ShouldBe("d_chk");
        check.Expression.ShouldBe("VALUE <> ''");
    }

    [Fact]
    public void Parse_AllClauses_CapturesEachInOrder()
    {
        // Arrange
        var domain = ParseDomain(

            // Act
            "CREATE DOMAIN app.email AS text NOT NULL CONSTRAINT email_fmt CHECK (VALUE ~ '@') DEFAULT 'x@y';");

        // Assert
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeTrue();
        domain.Checks.ShouldHaveSingleItem().Name.ShouldBe("email_fmt");
        domain.Default.ShouldBe("'x@y'");
    }

    [Fact]
    public void Parse_RenameDomain_BecomesADirective()
        => Directives("CREATE SCHEMA app; CREATE DOMAIN app.typeid AS text; RENAME DOMAIN app.legacy_id TO typeid;")
            .ObjectRenames.ShouldHaveSingleItem().From.Name.ShouldBe("legacy_id");

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseDomain("--- unique id\nCREATE DOMAIN app.typeid AS text;").Comment.ShouldBe("unique id");

    [Fact]
    public void Parse_DuplicateDomain_FailsTheRead()
        => new TestNsqlParser("CREATE SCHEMA app; CREATE DOMAIN app.d AS text; CREATE DOMAIN app.d AS int;").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialDomain_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE PARTIAL DOMAIN app.d AS text;").Parse())
            .Message.ShouldContain("after CREATE");

    [Fact]
    public void Parse_UnknownClause_Throws()
        => Should.Throw<NsqlSyntaxException>(() =>
            new TestNsqlParser("CREATE SCHEMA app; CREATE DOMAIN app.d AS text WIBBLE;").Parse())
            .Message.ShouldContain("Expected NOT NULL, NULL, CONSTRAINT");

    private static ProjectDirectives Directives(string source)
    {
        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }
}
