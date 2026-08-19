using NSchema.Model.Scripts;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class NsqlParserScriptTests
{
    private static IReadOnlyList<DeploymentScript> ReadScripts(string source) => new TestNsqlParser(source).Parse().Directives.DeploymentScripts;

    [Fact]
    public void Parse_PreDeployment_CapturesNameBodyAndType()
    {
        // Act
        var script = ReadScripts("SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$ CREATE EXTENSION IF NOT EXISTS citext; $$;")

        // Assert
            .ShouldHaveSingleItem();

        script.Name.ShouldBe("enable_citext");
        script.Phase.ShouldBe(DeploymentPhase.Pre);
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS citext;");
        script.RunOutsideTransaction.ShouldBeFalse();
    }

    [Fact]
    public void Parse_PostDeployment_CapturesType()
        => ReadScripts("SCRIPT backfill RUN ON POST DEPLOYMENT AS $$ UPDATE app.t SET x = 1; $$;")
            .ShouldHaveSingleItem().Phase.ShouldBe(DeploymentPhase.Post);

    [Fact]
    public void Parse_Body_PreservesInnerSemicolonsAndQuotes()
    {
        // Act
        // The dollar-quoted body is opaque: inner ';' and single quotes are part of the script, not terminators.
        var script = ReadScripts(
            """
            SCRIPT seed RUN ON POST DEPLOYMENT AS $$
                INSERT INTO app.t (name) VALUES ('a;b');
                UPDATE app.t SET name = '';
            $$;
            """).ShouldHaveSingleItem();

        // Assert
        script.Sql.ShouldBe("INSERT INTO app.t (name) VALUES ('a;b');\n    UPDATE app.t SET name = '';");
    }

    [Fact]
    public void Parse_BodyOnNextLine_IsAccepted()
        => ReadScripts(
            """
            SCRIPT x RUN ON PRE DEPLOYMENT AS
            $$ SELECT 1; $$;
            """).ShouldHaveSingleItem().Sql.ShouldBe("SELECT 1;");

    [Fact]
    public void Parse_RunOutsideTransactionOption_IsCaptured()
    {
        // Arrange
        var script = ReadScripts(

            // Act
            "SCRIPT concurrent_index RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$ CREATE INDEX CONCURRENTLY i ON app.t (c); $$;")

            // Assert
            .ShouldHaveSingleItem();

        script.RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Parse_CustomDollarTag_IsHonoured()
    {
        // A differently-tagged $$ inside the body is just content; only the opening tag closes it.
        var script = ReadScripts("SCRIPT x RUN ON PRE DEPLOYMENT AS $body$ SELECT $$nested$$; $body$;").ShouldHaveSingleItem();

        script.Sql.ShouldBe("SELECT $$nested$$;");
    }

    [Fact]
    public void Parse_ScriptsAndSchema_Coexist()
    {
        var document = new TestNsqlParser(
            """
            SCRIPT pre RUN ON PRE DEPLOYMENT AS $$ SELECT 1; $$;
            CREATE SCHEMA app;
            SCRIPT post RUN ON POST DEPLOYMENT AS $$ SELECT 2; $$;
            """).Parse();

        document.Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        document.Directives.DeploymentScripts.Select(s => (s.Name.Value, s.Phase)).ShouldBe(
            [("pre", DeploymentPhase.Pre), ("post", DeploymentPhase.Post)]);
    }

    [Fact]
    public void Parse_DeploymentScripts_AreNotPartOfTheSchema()
        => new TestNsqlParser("SCRIPT x RUN ON PRE DEPLOYMENT AS $$ SELECT 1; $$;").Parse().Database.Schemas.ShouldBeEmpty();

    [Fact]
    public void Parse_MissingDeploymentKeyword_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadScripts("SCRIPT x RUN ON PRE 'y' AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("DEPLOYMENT");

    [Fact]
    public void Parse_WrongTokenBeforeBody_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadScripts("SCRIPT x RUN ON PRE DEPLOYMENT WHEN $$ SELECT 1; $$;"))
            .Message.ShouldContain("AS");

    [Fact]
    public void Parse_BodyWithoutAs_Throws()
        // With no 'AS' anchor the body's '$' is reached as an unexpected character — still rejected.
        => Should.Throw<NsqlSyntaxException>(() => ReadScripts("SCRIPT x RUN ON PRE DEPLOYMENT $$ SELECT 1; $$;"));

    [Fact]
    public void Parse_UnknownOption_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadScripts("SCRIPT x RUN ON PRE DEPLOYMENT (whoops = true) AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("run_outside_transaction");

    [Fact]
    public void Parse_UnterminatedBody_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ReadScripts("SCRIPT x RUN ON PRE DEPLOYMENT AS $$ SELECT 1;"))
            .Message.ShouldContain("Unterminated");
}
