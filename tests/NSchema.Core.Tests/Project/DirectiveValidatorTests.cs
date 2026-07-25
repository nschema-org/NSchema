using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Project;

/// <summary>
/// The static directive rules — everything checkable against the declarations alone. Rules needing current
/// state (self-expiry) live with the differ.
/// </summary>
public sealed class DirectiveValidatorTests
{
    private static readonly SqlIdentifier _app = new("app");

    private static ObjectAddress App(string name) => new(_app, name);

    private static ProjectDefinition Project(ProjectDirectives directives, params Schema[] schemas) =>
        new(new Database { Schemas = [.. schemas] }, directives);

    private static Schema AppSchema(params Table[] tables) => new Schema { Name = _app, Tables = [.. tables] };

    private static Table Table(string name, params string[] columns) =>
        new Table { Name = name, Columns = [.. columns.Select(c => new Column { Name = c, Type = SqlType.Int })] };

    private static IReadOnlyList<Diagnostic> Validate(ProjectDefinition project) => [.. DirectiveValidator.Validate(project)];

    [Fact]
    public void Validate_WellFormedRename_ProducesNothing()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people")]),

            // Act
            AppSchema(Table("people", "id")));

        // Assert
        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RenameTargetNotDeclared_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people")]),

            // Act
            AppSchema());

        // Assert
        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("table", "app.users", "people"));
    }

    [Fact]
    public void Validate_RenameSourceStillDeclared_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people")]),

            // Act
            AppSchema(Table("people", "id"), Table("users", "id")));

        // Assert
        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameSourceStillDeclared("table", "app.users", "people"));
    }

    [Fact]
    public void Validate_DirectiveIntoUndeclaredSchema_IsAnError()
    {
        var rename = new ObjectRenameDirective(new ObjectAddress("ghost", "t") with { Kind = ObjectKind.Table }, "t2");
        var project = Project(new ProjectDirectives(ObjectRenames: [rename]));

        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveSchemaNotDeclared($"RENAME of table '{rename.From}'", "ghost"));
    }

    [Fact]
    public void Validate_SelfRename_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames: [new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "users")]),

            // Act
            AppSchema(Table("users", "id")));

        // Assert
        Validate(project).ShouldContain(ProjectDiagnostics.SelfRename("table", "app.users"));
    }

    [Fact]
    public void Validate_TwoRenamesSharingASource_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people"),
                    new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "members"),
                ]),

            // Act
            AppSchema(Table("people", "id"), Table("members", "id")));

        // Assert
        Validate(project).ShouldContain(ProjectDiagnostics.DuplicateRenameSource("table", "app.users"));
    }

    [Fact]
    public void Validate_TwoRenamesSharingATarget_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people"),
                    new ObjectRenameDirective(App("members") with { Kind = ObjectKind.Table }, "people"),
                ]),

            // Act
            AppSchema(Table("people", "id")));

        // Assert
        Validate(project).ShouldContain(ProjectDiagnostics.DuplicateRenameTarget("table", "app.people"));
    }

    [Fact]
    public void Validate_RenameChain_IsAnError()
    {
        // Arrange
        // a → b and b → c: renames are unordered, so the chain is ambiguous, whichever way it is written.
        var project = Project(new ProjectDirectives(
                ObjectRenames:
                [
                    new ObjectRenameDirective(App("a") with { Kind = ObjectKind.Table }, "b"),
                    new ObjectRenameDirective(App("b") with { Kind = ObjectKind.Table }, "c"),
                ]),

            // Act
            AppSchema(Table("b", "id"), Table("c", "id")));

        // Assert
        Validate(project).ShouldContain(ProjectDiagnostics.RenameChain("table", "app.b"));
    }

    [Fact]
    public void Validate_SameBareNamesInDifferentSchemas_DoNotInteract()
    {
        // Renames partition by container: two schemas each renaming their own 'users' is not a conflict.
        SqlIdentifier other = "audit";
        var project = new ProjectDefinition(
            new Database { Schemas = [AppSchema(Table("people", "id")), new Schema { Name = other, Tables = [Table("people", "id")] }] },
            new ProjectDirectives(ObjectRenames: [
                new ObjectRenameDirective(App("users") with { Kind = ObjectKind.Table }, "people"),
                new ObjectRenameDirective(new ObjectAddress(other, "users") with { Kind = ObjectKind.Table }, "people"),
            ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ObjectDirectivesUnderASchemaRename_ResolveThroughIt()
    {
        // The directives address current reality — schema 'sales', though the declaration is 'core' — so the
        // validator resolves the container through the schema rename before checking declarations.
        SqlIdentifier core = "core";
        SqlIdentifier sales = "sales";
        var project = new ProjectDefinition(
            new Database { Schemas = [new Schema { Name = core, Tables = [Table("people", "id", "full_name")] }] },
            new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(new SchemaAddress(sales), new SchemaAddress(core))],
                ObjectRenames: [new ObjectRenameDirective(new ObjectAddress(sales, "users") with { Kind = ObjectKind.Table }, "people")],
                MemberRenames:
                [
                    new MemberRenameDirective(new MemberAddress(sales, "users", "name"), "full_name"),
                ]));

        Validate(project).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnRenameTargetNotDeclared_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, "users", "name"), "full_name")]),

            // Act
            AppSchema(Table("users", "id")));

        // Assert
        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.RenameTargetNotDeclared("column", "app.users.name", "full_name"));
    }

    [Fact]
    public void Validate_ColumnRenameIntoUndeclaredTable_IsAnError()
    {
        // Arrange
        var project = Project(new ProjectDirectives(
                MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, "ghost", "name"), "full_name")]),

            // Act
            AppSchema());

        // Assert
        Validate(project).ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DirectiveTableNotDeclared(new MemberAddress(_app, "ghost", "name")));
    }
}
