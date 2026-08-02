using System.Text;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using NSchema.State;
using NSchema.State.Domain;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Covers the full-document round-trip (schema + scripts)
/// that the <c>fmt</c> command depends on for losslessness.
/// </summary>
public sealed class ProjectedDocumentWriterTests
{
    // Canonicalize the schema half via the internal state serializer (independent of the writer under test).
    private static string Canonical(Database schema)
        => Encoding.UTF8.GetString(new DatabaseStateSerializer().Serialize(new DatabaseState(schema)).Span);

    private static void AssertEquivalent(ProjectDefinition expected, ProjectDefinition actual)
    {
        Canonical(actual.Database).ShouldBe(Canonical(expected.Database));
        actual.Directives.ChangeScripts.ShouldBe(expected.Directives.ChangeScripts);
        actual.Directives.DeploymentScripts.ShouldBe(expected.Directives.DeploymentScripts);
    }

    private static string Write(ProjectDefinition document) => NsqlWriter.Write(document.Database, document.Directives);

    // Round-trip a source through Read -> Write -> Read, asserting the document survives and that a second
    // Write produces byte-identical output (formatting is idempotent — the property `fmt --check` relies on).
    private static string AssertRoundTrips(string source)
    {
        var document = new TestNsqlParser(source).Parse();
        var formatted = Write(document);

        var reparsed = new TestNsqlParser(formatted).Parse();
        AssertEquivalent(document, reparsed);
        Write(reparsed).ShouldBe(formatted);

        return formatted;
    }

    // -------------------------------------------------------------------------
    // Deployment scripts
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PreAndPostScripts_RoundTrip()
    {
        // Act
        var formatted = AssertRoundTrips(
            """
            SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$
                CREATE EXTENSION IF NOT EXISTS citext;
            $$;

            SCRIPT reindex RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$
                CREATE INDEX CONCURRENTLY idx ON app.t (name);
            $$;
            """);

        // Assert
        formatted.ShouldContain("SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$");
        formatted.ShouldContain("SCRIPT reindex RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$");
    }

    [Fact]
    public void Write_RunOnceScript_RoundTrips()
        => AssertRoundTrips("SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO app.c VALUES (1); $$;")
            .ShouldContain("SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$");

    [Fact]
    public void Write_ScriptBodyContainingDoubleDollar_PicksASafeTag()
    {
        // Act
        var formatted = AssertRoundTrips("SCRIPT x RUN ON PRE DEPLOYMENT AS $outer$ SELECT '$$' AS v $outer$;");

        // Assert
        // The default $$ delimiter would collide with the body, so the writer must choose a tagged delimiter.
        formatted.ShouldNotContain("AS $$");
        formatted.ShouldContain("$body1$");
    }

    // -------------------------------------------------------------------------
    // Data migrations
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_NamedMigration_RoundTrips()
        => AssertRoundTrips(
            """
            SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$
                UPDATE app.users SET email = '';
            $$;
            """).ShouldContain("SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$");

    [Fact]
    public void Write_ConstraintMigration_RoundTrips()
        => AssertRoundTrips(
            """
            SCRIPT dedupe RUN ON ADD CONSTRAINT app.orders.total_positive AS $$
                DELETE FROM app.orders WHERE total <= 0;
            $$;
            """).ShouldContain("SCRIPT dedupe RUN ON ADD CONSTRAINT app.orders.total_positive AS $$");

    [Fact]
    public void Write_MigrationWithRunOutsideTransaction_RoundTrips()
        => AssertRoundTrips(
            "SCRIPT retype RUN ON ALTER COLUMN TYPE app.orders.total (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .ShouldContain("SCRIPT retype RUN ON ALTER COLUMN TYPE app.orders.total(run_outside_transaction = true) AS $$");

    [Fact]
    public void Write_MigrationBodyContainingDoubleDollar_PicksASafeTag()
    {
        // Act
        var formatted = AssertRoundTrips("SCRIPT x RUN ON ADD COLUMN app.t.c AS $outer$ SELECT '$$' AS v $outer$;");

        // Assert
        // The default $$ delimiter would collide with the body, so the writer must choose a tagged delimiter.
        formatted.ShouldNotContain("AS $$");
        formatted.ShouldContain("$body1$");
    }

    // -------------------------------------------------------------------------
    // Full document
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_FullDocument_RoundTripsAndIsIdempotent()
        => AssertRoundTrips(
            """
            CREATE SCHEMA app;

            CREATE TABLE app.users
            (
                id bigint NOT NULL IDENTITY,
                name text NOT NULL,
                CONSTRAINT users_pkey PRIMARY KEY (id)
            );

            SCRIPT seed RUN ON POST DEPLOYMENT AS $$
                INSERT INTO app.users (name) VALUES ('root');
            $$;
            """);

    [Fact]
    public void Write_EmitsSchemaThenScripts_RegardlessOfSourceOrder()
    {
        // Arrange
        // Source deliberately interleaves: script, then schema.
        var document = new TestNsqlParser(
            """
            SCRIPT seed RUN ON POST DEPLOYMENT AS $$ SELECT 1 $$;

            CREATE SCHEMA app;
            """).Parse();
        var formatted = Write(document);

        var schema = formatted.IndexOf("CREATE SCHEMA app", StringComparison.Ordinal);

        // Act
        var script = formatted.IndexOf("POST DEPLOYMENT", StringComparison.Ordinal);

        // Assert
        schema.ShouldBeLessThan(script);
    }

    [Fact]
    public void Write_DatabaseOverload_EmitsNoScripts()
    {
        var ddl = NsqlWriter.Write(new Database { Schemas = [new Schema { Name = "app" }] });

        ddl.ShouldNotContain("DEPLOYMENT");
        ddl.ShouldBe("CREATE SCHEMA app;\n");
    }

    [Fact]
    public void Write_ImplicitSchema_IsNotDeclared()
    {
        // Arrange — a schema the database owns, carrying settings of its own.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "public",
                    IsImplicit = true,
                    Comment = "standard public schema",
                    Grants = [new SchemaGrant("api")],
                    Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }],
                },
            ],
        };

        // Act
        var ddl = NsqlWriter.Write(database);

        // Assert — writing the declaration would produce a project a provider then rejects.
        ddl.ShouldNotContain("CREATE SCHEMA");
        ddl.ShouldNotContain("GRANT USAGE");
        ddl.ShouldContain("CREATE TABLE public.users");
    }
}
