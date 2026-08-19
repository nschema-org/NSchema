using NSchema.Model;
using NSchema.Project;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// Cross-file assembly: one accumulator spans every document, so declarations merge, dedup, and resolve
/// project-wide — the file boundary is organization, not semantics.
/// </summary>
public sealed class ProjectAssemblerTests
{
    private static NsqlDiagnostic SingleError(Result<NSchema.Project.Domain.Directives.ProjectDefinition> result)
        => result.Errors.ShouldHaveSingleItem().ShouldBeOfType<NsqlDiagnostic>();

    // ── Aggregation across files ──────────────────────────────────────────────

    [Fact]
    public void Assemble_DistinctSchemasAcrossFiles_ProducesAllSchemas()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",

            // Act
            "CREATE SCHEMA admin; CREATE TABLE admin.roles (id int);").Require();

        // Assert
        result.Database.Schemas.Select(s => s.Name).ShouldBe(["app", "admin"]);
    }

    [Fact]
    public void Assemble_SameSchemaAcrossFiles_MergesObjects()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int); CREATE VIEW app.active AS SELECT 1 FROM app.users;",

            // Act
            "CREATE TABLE app.posts (id int); CREATE VIEW app.recent AS SELECT 1 FROM app.posts;").Require();

        // Assert
        var schema = result.Database.Schemas.ShouldHaveSingleItem();
        schema.Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
        schema.Views.Select(v => v.Name).ShouldBe(["active", "recent"]);
    }

    [Fact]
    public void Assemble_SchemaDeclaredInOneFile_CarriesItsCommentToTheMerge()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "--- App schema\nCREATE SCHEMA app;",

            // Act
            "CREATE TABLE app.posts (id int);").Require();

        // Assert
        var schema = result.Database.Schemas.ShouldHaveSingleItem();
        schema.Comment.ShouldBe("App schema");
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("posts");
    }

    [Fact]
    public void Assemble_SchemaGrantsAcrossFiles_UnionAndDedup()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; GRANT USAGE ON SCHEMA app TO app_user;",

            // Act
            "GRANT USAGE ON SCHEMA app TO reporting; GRANT USAGE ON SCHEMA app TO app_user;").Require();

        // Assert
        result.Database.Schemas.Single().Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    // ── Cross-file duplicates — positioned at the offending re-declaration ────

    [Fact]
    public void Assemble_DuplicateTableAcrossFiles_IsAnError()
    {
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "CREATE TABLE app.users (id int);");

        var error = SingleError(result);
        error.ShouldBe(ProjectDiagnostics.ObjectAlreadyDeclared(
            SchemaObjectKind.Table, "app", "users", error.Position) with
        { File = "file2.sql" });
        error.Position.Line.ShouldBe(1);
    }

    [Fact]
    public void Assemble_DuplicateViewAcrossFiles_IsAnError()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1 FROM app.t;",
            "CREATE VIEW app.v AS SELECT 2 FROM app.t;");

        // Act
        var error = SingleError(result);

        // Assert
        error.ShouldBe(ProjectDiagnostics.ObjectAlreadyDeclared(
            SchemaObjectKind.View, "app", "v", error.Position) with
        { File = "file2.sql" });
    }

    [Fact]
    public void Assemble_DuplicateRoutineAcrossFiles_IsAnError()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$;",

            // Act
            "CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 2 $$;");

        // Assert
        SingleError(result).Message.ShouldContain("Routine 'app.f' is already declared");
    }

    [Fact]
    public void Assemble_FunctionAndProcedureAcrossFiles_ShareOneNameSpace()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE FUNCTION app.r() RETURNS int AS $$ SELECT 1 $$;",

            // Act
            "CREATE PROCEDURE app.r() AS $$ SELECT 1 $$;");

        // Assert
        SingleError(result).Message.ShouldContain("Routine 'app.r' is already declared");
    }

    [Fact]
    public void Assemble_DuplicateSchemaDeclarationAcrossFiles_IsAnError()
    {
        // Arrange
        // Only the declaration is unique: objects land in a schema from any file without redeclaring it,
        // so a second CREATE SCHEMA is a duplicate wherever it lives.
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app;",
            "CREATE SCHEMA app;");

        // Act
        var error = SingleError(result);

        // Assert
        error.ShouldBe(ProjectDiagnostics.SchemaAlreadyDeclared("app", error.Position) with
        { File = "file2.sql" });
    }

    [Fact]
    public void Assemble_DuplicateExtensionAcrossFiles_IsAnError()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE EXTENSION citext;",
            "CREATE EXTENSION citext;");

        // Act
        var error = SingleError(result);

        // Assert
        error.ShouldBe(ProjectDiagnostics.ExtensionAlreadyDeclared("citext", error.Position) with
        { File = "file2.sql" });
    }

    // ── Standalone statements resolve project-wide, not per file ──────────────

    [Fact]
    public void Assemble_TriggerInAnotherFile_AttachesToItsTable()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "CREATE TRIGGER audit AFTER INSERT ON app.users EXECUTE FUNCTION app.f();").Require();

        // Act
        var table = result.Database.Schemas.Single().Tables.Single();

        // Assert
        table.Triggers.ShouldHaveSingleItem().Name.ShouldBe("audit");
    }

    [Fact]
    public void Assemble_TableGrantInAnotherFile_AttachesToItsTable()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "GRANT SELECT ON app.users TO readers;").Require();

        // Act
        var table = result.Database.Schemas.Single().Tables.Single();

        // Assert
        table.Grants.ShouldHaveSingleItem().Role.ShouldBe("readers");
    }

    [Fact]
    public void Assemble_SameTableGrantInTwoFiles_IsOneGrant()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int); GRANT SELECT ON app.users TO readers;",

            // Act
            "GRANT SELECT ON app.users TO readers;").Require();

        // Assert
        result.Database.Schemas.Single().Tables.Single().Grants.ShouldHaveSingleItem();
    }

    [Fact]
    public void Assemble_StandaloneIndexInAnotherFile_AttachesToItsTable()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int, email text);",
            "CREATE INDEX users_email_ix ON app.users (email);").Require();

        // Act
        var table = result.Database.Schemas.Single().Tables.Single();

        // Assert
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Assemble_UnknownGrantTable_CarriesTheGrantingFile()
    {
        // Arrange
        var result = TestNsqlParser.Assemble(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int);",
            "GRANT SELECT ON app.ghost TO readers;");

        // Act
        var error = SingleError(result);

        // Assert
        error.ShouldBe(ProjectDiagnostics.UnknownGrantTable(
            "app", "ghost", error.Position) with
        { File = "file2.sql" });
    }
}
