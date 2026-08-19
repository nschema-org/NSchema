using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Foundation coverage for the token-bearing node layer: a parsed leaf node reprints its own source via
/// <see cref="NsqlNode.ToSource"/>, walking its tokens (raw text + trivia) — the seed of the tree-wide
/// <c>Print(Parse(s)) == s</c> property that structural nodes will extend.
/// </summary>
public sealed class NsqlNodeRoundTripTests
{
    private static T Parse<T>(string source) where T : NsqlStatement
    {
        var result = NsqlReader.Read(source);
        result.Value.ShouldNotBeNull();
        return result.Value.Statements.ShouldHaveSingleItem().ShouldBeOfType<T>();
    }

    [Fact]
    public void Identifier_ReprintsBareName()
    {
        // Arrange / Act
        var statement = Parse<CreateSchemaStatement>("CREATE SCHEMA foo;");

        // Assert
        statement.Name.Value.ShouldBe("foo");
        statement.Name.Token.Raw.ShouldBe("foo");
        statement.Name.ToSource().ShouldBe("foo");
    }

    [Fact]
    public void Identifier_DecodesValueButReprintsRawQuotes()
    {
        // Arrange / Act — a quoted identifier: Value is decoded, but the printer must reproduce the raw delimiters.
        var statement = Parse<CreateSchemaStatement>("""CREATE SCHEMA "My Schema";""");

        // Assert
        statement.Name.Value.ShouldBe("My Schema");
        statement.Name.ToSource().ShouldBe("\"My Schema\"");
    }

    [Fact]
    public void QualifiedName_ReprintsSchemaDotName()
    {
        // Arrange / Act
        var statement = Parse<CreateTableStatement>("CREATE TABLE app.users(id bigint not null);");

        // Assert
        statement.Name.Schema!.Value.ShouldBe("app");
        statement.Name.Name.Value.ShouldBe("users");
        statement.Name.ToSource().ShouldBe("app.users");
    }

    [Fact]
    public void QualifiedName_ReprintsQuotedParts()
    {
        // Arrange / Act
        var statement = Parse<CreateTableStatement>("""CREATE TABLE "app"."Order Details"(id bigint not null);""");

        // Assert
        statement.Name.ToSource().ShouldBe("\"app\".\"Order Details\"");
    }

    // -------------------------------------------------------------------------
    // Document-level round-trip, over the statement kinds already lowered to tokens (CREATE SCHEMA so far).
    // As each statement family gains a Children override, its fixtures move into this corpus.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("CREATE SCHEMA foo;\n")]
    [InlineData("create schema foo;")]
    [InlineData("CREATE SCHEMA \"My Schema\";\n")]
    [InlineData("--- Billing.\nCREATE SCHEMA billing;\n")]
    [InlineData("CREATE SCHEMA a;\n\nCREATE SCHEMA b;\n")]
    [InlineData("  CREATE   SCHEMA   spaced ;\n")]
    [InlineData("CREATE SCHEMA foo; -- trailing note\n")]
    [InlineData("-- leading comment\nCREATE SCHEMA foo;\n\n-- trailing comment\n")]
    [InlineData("CREATE SCHEMA a;\r\nCREATE SCHEMA b;\r\n")]
    [InlineData("CREATE EXTENSION citext;\n")]
    [InlineData("create extension \"uuid-ossp\";\n")]
    [InlineData("CREATE EXTENSION postgis VERSION '3.4';\n")]
    [InlineData("--- Fuzzy search.\nCREATE EXTENSION   pg_trgm   VERSION   'x' ;\n")]
    [InlineData("CREATE SCHEMA app;\nCREATE EXTENSION citext;\n")]
    [InlineData("GRANT USAGE ON SCHEMA app TO reader;\n")]
    [InlineData("grant   usage   on   schema   app   to   reader ;\n")]
    [InlineData("RENAME SCHEMA old TO new;\n")]
    [InlineData("RENAME COLUMN app.users.email TO email_address;\n")]
    [InlineData("--- Fix a typo.\nRENAME COLUMN \"app\".\"users\".\"emial\" TO email;\n")]
    [InlineData("CREATE SCHEMA app;\nGRANT USAGE ON SCHEMA app TO reader;\n")]
    // Enum — punctuated value list.
    [InlineData("CREATE ENUM app.status ('pending', 'sent');\n")]
    [InlineData("create enum app.status('a','b','c');\n")]
    [InlineData("CREATE ENUM app.status (  'pending' ,  'sent'  );\n")]
    [InlineData("--- Delivery states.\nCREATE ENUM app.status ('pending', 'it''s sent');\n")]
    // View — opaque body span, verbatim.
    [InlineData("CREATE VIEW app.active AS SELECT * FROM app.users WHERE active;\n")]
    [InlineData("create materialized view app.mv as select 1;\n")]
    [InlineData("CREATE VIEW app.v AS\n  SELECT id,\n         name\n  FROM app.t\n;\n")]
    // Composite type — field list, exercising TypeName arguments.
    [InlineData("CREATE TYPE app.point AS (x integer, y integer);\n")]
    [InlineData("create type app.money as (amount numeric(10,2), currency varchar(3));\n")]
    [InlineData("CREATE TYPE app.t AS ( a  app.status , b text );\n")]
    // Rename object — single and multi-keyword kinds.
    [InlineData("RENAME TABLE app.users TO people;\n")]
    [InlineData("rename materialized view app.mv TO mv2;\n")]
    [InlineData("RENAME FUNCTION app.fn TO gn;\n")]
    // Routine — parenthesised arg span + definition span.
    [InlineData("CREATE FUNCTION app.add(a int, b int) RETURNS int AS $$ SELECT a + b $$;\n")]
    [InlineData("create procedure app.noop() as $$ begin end $$;\n")]
    [InlineData("CREATE FUNCTION app.f( x text )\n  RETURNS text\n  AS $$ SELECT x $$\n;\n")]
    // Sequence — options clause as a verbatim interior span.
    [InlineData("CREATE SEQUENCE app.s;\n")]
    [InlineData("CREATE SEQUENCE app.s (START 1, INCREMENT 2);\n")]
    [InlineData("create sequence app.s ( as bigint , minvalue 0 , cycle );\n")]
    // Grant table — privilege list.
    [InlineData("GRANT SELECT, INSERT ON app.users TO reader;\n")]
    [InlineData("grant select , update , delete on app.users to writer;\n")]
    // Domain — tail clauses printed verbatim, in source order.
    [InlineData("CREATE DOMAIN app.pos AS integer;\n")]
    [InlineData("CREATE DOMAIN app.email AS text NOT NULL CONSTRAINT c CHECK (value LIKE '%@%');\n")]
    [InlineData("create domain app.d as int CONSTRAINT c CHECK (value > 0) NOT NULL DEFAULT 0;\n")]
    // Tables — columns with modifiers, constraints, multi-line bodies.
    [InlineData("CREATE TABLE app.t (id bigint);\n")]
    [InlineData("CREATE TABLE app.users (\n  id bigint not null identity,\n  email text not null,\n  created_at timestamptz default now()\n);\n")]
    [InlineData("create table app.t(a int, b int, constraint pk primary key(a));\n")]
    [InlineData("CREATE TABLE app.orders (\n  id bigint not null,\n  user_id bigint not null,\n  constraint pk_orders primary key (id),\n  constraint fk_user foreign key (user_id) references app.users (id) on delete cascade\n);\n")]
    [InlineData("CREATE TABLE app.t (\n  code text,\n  constraint uq unique (code),\n  constraint ck check (length(code) > 0)\n);\n")]
    [InlineData("CREATE TABLE app.t ( x numeric(10,2) not null,  y app.status  default 'a' );\n")]
    // Inline index members.
    [InlineData("CREATE TABLE app.t (\n  id bigint not null,\n  email text,\n  unique index ix_email (email),\n  index ix_lower using btree ((lower(email)) desc nulls last) where (email is not null)\n);\n")]
    // Standalone indexes.
    [InlineData("CREATE INDEX ix_users_email ON app.users (email);\n")]
    [InlineData("create unique index ix ON app.t using gist (a asc, (lower(b)) desc) include (c, d) where (a > 0);\n")]
    // Exclusion constraint.
    [InlineData("CREATE TABLE app.rooms (\n  during tsrange,\n  constraint no_overlap exclude using gist (during with &&)\n);\n")]
    // Scripts — deployment & change events, options, dollar bodies.
    [InlineData("SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$\nCREATE EXTENSION IF NOT EXISTS citext;\n$$;\n")]
    [InlineData("script seed run once on post deployment as $$ insert into app.t values (1); $$;\n")]
    [InlineData("SCRIPT backfill RUN ON ADD COLUMN app.users.email (run_outside_transaction = true) AS $$\nUPDATE app.users SET email = '';\n$$;\n")]
    [InlineData("script noop run on alter column type app.users.id as $$ select 1 $$;\n")]
    // Triggers — execute-function and inline-body actions.
    [InlineData("CREATE TRIGGER trg_audit AFTER INSERT OR UPDATE ON app.users FOR EACH ROW EXECUTE FUNCTION app.audit();\n")]
    [InlineData("create trigger t before update of (email, name) on app.users when (old.email is distinct from new.email) execute procedure app.log(1, 'x');\n")]
    [InlineData("CREATE TRIGGER t INSTEAD OF DELETE ON app.v FOR EACH ROW AS $$\nBEGIN\n  RAISE;\nEND;\n$$;\n")]
    // Templates — nested statements / members, and apply.
    [InlineData("template audit_columns for table begin\n  created_at timestamptz not null,\n  updated_at timestamptz not null  -- touched on write\nend;\n")]
    [InlineData("--- Outbox.\ntemplate outbox begin\n  create enum outbox_status ('pending', 'sent');\n  create table outbox (id uuid not null);\nend;\n")]
    [InlineData("apply template outbox in schema billing,   ordering;\n")]
    // Dangling doc-comments (valid, but attached to no statement) still round-trip.
    [InlineData("create schema a;\n--- a dangling doc comment\n")]
    [InlineData("--- only a doc comment, no statement")]
    public void Document_RoundTripsThroughToSource(string source)
    {
        // Act
        var document = NsqlReader.Read(source).Value.ShouldNotBeNull();

        // Assert
        document.ToSource().ShouldBe(source);
    }

    [Theory]
    [InlineData("PLUGIN pg (source = 'NSchema.Postgres', version = '1.0');\n")]
    [InlineData("engine ( parallelism = 4 );\n")]
    [InlineData("DATABASE postgres (connection = 'host=localhost', pool.max = 10);\n")]
    [InlineData("-- config\nPLUGIN pg (source = 'x');\n\nENGINE (dry_run = true);\n")]
    public void ConfigurationDocument_RoundTripsThroughToSource(string source)
    {
        // Act
        var document = NsqlReader.Read(source).Value.ShouldNotBeNull();

        // Assert
        document.ToSource().ShouldBe(source);
    }

    // -------------------------------------------------------------------------
    // Error recovery: a document with syntax errors still round-trips — the skipped tokens ride as trivia on the
    // next token (or the end-of-file), so lint/format works on broken files.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("create schema a;\nCREATE BOGUS HERE;\ncreate schema b;\n")]
    [InlineData("CREATE SCHEMA app;\nCREATE BOGUS\n")]
    [InlineData("  ??? !!! ;\nCREATE SCHEMA ok;\n")]
    public void Document_WithSyntaxErrors_StillRoundTrips(string source)
    {
        // Act
        var result = NsqlReader.Read(source);

        // Assert — errors are reported, but the document reprints its full source, junk included.
        result.Diagnostics.ShouldNotBeEmpty();
        result.Value.ShouldNotBeNull().ToSource().ShouldBe(source);
    }

    [Fact]
    public void LockDocument_RoundTripsThroughToSource()
    {
        // Arrange
        const string source = "LOCK (source = 'NSchema.Postgres', version = '1.2.3');\n";

        // Act
        var document = NsqlReader.Read(source).Value.ShouldNotBeNull();

        // Assert
        document.ToSource().ShouldBe(source);
    }
}
