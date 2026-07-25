using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

/// <summary>
/// The alignment pass: rename directives rewrite the current schema into the declared name-space before the
/// compare, keeping a log of what moved. Ambiguity is decided here — an ambiguous rename is skipped with an
/// error diagnostic (the error blocks application), and an already-applied rename expires with an info.
/// </summary>
public sealed class DatabaseAlignerTests
{
    private static readonly SqlIdentifier _app = new("app");

    private static Database Db(params Schema[] schemas) => new Database { Schemas = [.. schemas] };

    private static Table T(string name, params string[] columns) =>
        new Table { Name = name, Columns = [.. columns.Select(c => new Column { Name = c, Type = SqlType.Int })] };

    private static ProjectDirectives TableRename(string from, string to) =>
        new(ObjectRenames: [new ObjectRenameDirective(new ObjectAddress(_app, from, ObjectKind.Table), to)]);

    private static ProjectDirectives ColumnRename(string from, string to, string table = "t") =>
        new(MemberRenames: [new MemberRenameDirective(new MemberAddress(_app, table, from), to)]);

    [Fact]
    public void Align_NoDirectives_ReturnsTheCurrentTreeUntouched()
    {
        // Arrange
        var current = Db(new Schema { Name = _app, Tables = [T("users", "id")] });

        // Act
        var result = DatabaseAligner.Align(current, Db(), ProjectDirectives.Empty);

        // Assert — nothing to apply: the same tree comes back, with an empty log.
        result.IsSuccess.ShouldBeTrue();
        result.Require().Database.ShouldBeSameAs(current);
        result.Require().Renames.RenamedFrom(new ObjectAddress(_app, "users", ObjectKind.Table)).ShouldBeNull();
    }

    [Fact]
    public void Align_SchemaRename_RewritesTheSchemaAndLogsIt()
    {
        // Arrange
        var current = Db(new Schema { Name = "old_app", Tables = [T("users", "id")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("users", "id")] });
        var directives = new ProjectDirectives(SchemaRenames: [new SchemaRenameDirective(new SchemaAddress("old_app"), new SchemaAddress(_app))]);

        // Act
        var result = DatabaseAligner.Align(current, desired, directives);

        // Assert — the schema (and everything in it) now lives under the declared name; the log keys by it.
        result.IsSuccess.ShouldBeTrue();
        var aligned = result.Require();
        aligned.Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe(_app);
        aligned.Renames.RenamedFrom(new SchemaAddress(_app)).ShouldBe("old_app");
    }

    [Fact]
    public void Align_TableRename_RewritesTheTableAndLogsIt()
    {
        // Arrange
        var current = Db(new Schema { Name = _app, Tables = [T("people", "id")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("users", "id")] });

        // Act
        var result = DatabaseAligner.Align(current, desired, TableRename("people", "users"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var aligned = result.Require();
        aligned.Database.Schemas.Single().Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
        aligned.Renames.RenamedFrom(new ObjectAddress(_app, "users", ObjectKind.Table)).ShouldBe("people");
    }

    [Fact]
    public void Align_ColumnRename_RewritesTheColumnAndLogsIt()
    {
        // Arrange
        var current = Db(new Schema { Name = _app, Tables = [T("t", "mail")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("t", "email")] });

        // Act
        var result = DatabaseAligner.Align(current, desired, ColumnRename("mail", "email"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var aligned = result.Require();
        aligned.Database.Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem().Name.ShouldBe("email");
        aligned.Renames.RenamedFrom(new MemberAddress(_app, "t", "email")).ShouldBe("mail");
    }

    [Fact]
    public void Align_TableRenamedButOldNameStillDeclared_ErrorsAndSkipsTheRename()
    {
        // Arrange — 'people' is renamed to 'users' while 'people' is also still declared. This is
        // indistinguishable from "keep people, add users", so the rename is skipped with an error.
        var current = Db(new Schema { Name = _app, Tables = [T("people", "id")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("users", "id"), T("people", "id")] });

        // Act
        var result = DatabaseAligner.Align(current, desired, TableRename("people", "users"));

        // Assert — the error blocks application; the tree comes back unaligned.
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.AmbiguousRenameSourceStillDeclared("table", "app.users", "people"));
        result.Require().Database.Schemas.Single().Tables.ShouldHaveSingleItem().Name.ShouldBe("people");
    }

    [Fact]
    public void Align_TableRenamedOntoExistingName_ErrorsAndSkipsTheRename()
    {
        // Arrange — renaming 'a' to 'b' while a distinct 'b' already exists collides on the target name.
        var current = Db(new Schema { Name = _app, Tables = [T("a", "id"), T("b", "id")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("b", "id")] });

        // Act
        var result = DatabaseAligner.Align(current, desired, TableRename("a", "b"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.AmbiguousRenameTargetTaken("table", "app.b", "a", "b"));
    }

    [Fact]
    public void Align_ColumnRenamedButOldNameStillDeclared_ErrorsAndSkipsTheRename()
    {
        // Arrange
        var current = Db(new Schema { Name = _app, Tables = [T("t", "mail")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("t", "email", "mail")] });

        // Act
        var result = DatabaseAligner.Align(current, desired, ColumnRename("mail", "email"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.AmbiguousRenameSourceStillDeclared("column", "app.t.email", "mail"));
    }

    [Fact]
    public void Align_ObjectRenameInsideRenamedSchema_LogsUnderTheDeclaredSchemaName()
    {
        // Arrange — the object directive addresses current reality ('old_app.people'), but the log must key by
        // where the entity lands: the declared schema and name.
        var current = Db(new Schema { Name = "old_app", Tables = [T("people", "id")] });
        var desired = Db(new Schema { Name = _app, Tables = [T("users", "id")] });
        var directives = new ProjectDirectives(
            SchemaRenames: [new SchemaRenameDirective(new SchemaAddress("old_app"), new SchemaAddress(_app))],
            ObjectRenames: [new ObjectRenameDirective(new ObjectAddress("old_app", "people", ObjectKind.Table), "users")]);

        // Act
        var result = DatabaseAligner.Align(current, desired, directives);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var aligned = result.Require();
        var schema = aligned.Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe(_app);
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
        aligned.Renames.RenamedFrom(new ObjectAddress(_app, "users", ObjectKind.Table)).ShouldBe("people");
    }
}
