using NSchema.Deployment;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Operations;

public sealed class ImportOperationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly IDatabaseProvider _database = Substitute.For<IDatabaseProvider>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    // Tables carry a column because the DDL grammar has no empty-table form.
    private static Table MakeTable(string name) => new Table { Name = name, Columns = [new Column { Name = "id", Type = SqlType.Int }] };

    private readonly Database _schema = new Database
    {
        Schemas = [new Schema { Name = "app",
        Tables = [MakeTable("users"), MakeTable("orders")] }],
    };

    public ImportOperationTests()
    {
        Directory.CreateDirectory(_dir);
        Source(_schema);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Source(Database schema)
    {
        _database
            .GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(schema));
    }

    private ImportOperation BuildSut() => new(_database, _progress);

    private Task Execute(ImportArguments arguments) =>
        BuildSut().Execute(arguments, TestContext.Current.CancellationToken);

    private static async Task<Database> ReadSchema(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        return new TestNsqlParser(text).Parse().Database;
    }

    // Both extensions, because import keeps an existing file where it already lives.
    private static IEnumerable<string> ProjectFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.nsql", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories));

    private string ObjectPath(string type, string name) => Path.Combine(_dir, "app", type, $"{name}.nsql");
    private string HeaderPath => Path.Combine(_dir, "app", "schema.nsql");

    // Assembles every project file written under the output directory, as the project provider would.
    private async Task<Database> ReadAll()
    {
        var sources = new List<string>();
        foreach (var file in ProjectFiles(_dir))
        {
            sources.Add(await File.ReadAllTextAsync(file));
        }
        return TestNsqlParser.Assemble([.. sources]).Require().Database;
    }

    // ── Result payload ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ReturnsTheImportedSchemaAndEveryWrittenFile()
    {
        // Act
        var result = await BuildSut().Execute(new ImportArguments { OutputDirectory = _dir }, TestContext.Current.CancellationToken);

        // Assert — the result reports what was read and exactly which files it wrote (the schema header + one per table).
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Database.ShouldBe(_schema);
        result.Value!.WrittenFiles.ShouldBe([HeaderPath, ObjectPath("tables", "users"), ObjectPath("tables", "orders")], ignoreOrder: true);
    }

    // ── Source fetching ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_FetchesSchemaFromOnlineSource()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        await _database.Received(1).GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesScopeFilterToSource()
    {
        // Arrange
        var arguments = new ImportArguments { OutputDirectory = _dir, Scope = PlanningScope.To(DatabaseAddress.Schema("app"), DatabaseAddress.Schema("audit")) };

        // Act
        await Execute(arguments);

        // Assert
        await _database.Received(1).GetDatabase(arguments.Scope, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReportsVerboseCensusAndPerFileWrites()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // A census of what was fetched...
        _progress.Received(1).Report(OperationProgress.Detail("Fetched 1 schema, 2 tables from the database."));
        // ...and a line per object file, marked "Wrote" because nothing existed to merge into.
        _progress.Received(1).Report(OperationProgress.Detail($"Wrote {ObjectPath("tables", "users")}."));
        _progress.Received(1).Report(OperationProgress.Detail($"Wrote {ObjectPath("tables", "orders")}."));
    }

    [Fact]
    public async Task Execute_ReImport_ReportsMergeIntoExistingFile()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });
        _progress.ClearReceivedCalls();

        // A second import of the same object merges into the file written by the first.
        await Execute(new ImportArguments { OutputDirectory = _dir });

        _progress.Received(1).Report(OperationProgress.Detail($"Merged into {ObjectPath("tables", "users")}."));
    }

    [Fact]
    public async Task Execute_MergesIntoAnExistingFile_WhateverExtensionItCarries()
    {
        // A project imported before the extension existed holds its objects in .sql files. Writing the .nsql
        // sibling instead would leave the same table declared twice, so the existing file is what gets merged.
        var existing = Path.Combine(_dir, "app", "tables", "users.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "CREATE TABLE app.users (id int NOT NULL);", TestContext.Current.CancellationToken);

        await Execute(new ImportArguments { OutputDirectory = _dir });

        File.Exists(existing).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "users")).ShouldBeFalse();
        (await ReadSchema(existing)).Schemas.Single().Tables.Single().Name.ShouldBe("users");
    }

    [Fact]
    public async Task Execute_WritesNewObjectsAsNsql_AlongsideTheFilesItKept()
    {
        // Arrange — one object already lives in a .sql file, the other has never been imported.
        var existing = Path.Combine(_dir, "app", "tables", "users.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "CREATE TABLE app.users (id int NOT NULL);", TestContext.Current.CancellationToken);

        // Act
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Assert
        File.Exists(existing).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_CreatesOutputDirectoryIfMissing()
    {
        var nested = Path.Combine(_dir, "nested", "deep");

        await Execute(new ImportArguments { OutputDirectory = nested });

        File.Exists(Path.Combine(nested, "app", "tables", "users.nsql")).ShouldBeTrue();
    }

    // ── Object layout ───────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_CreatesOneFilePerMajorObjectGroupedByType()
    {
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        File.Exists(ObjectPath("tables", "users")).ShouldBeTrue();
        File.Exists(ObjectPath("tables", "orders")).ShouldBeTrue();
        File.Exists(ObjectPath("views", "active")).ShouldBeTrue();
        File.Exists(ObjectPath("routines", "calc")).ShouldBeTrue();
        File.Exists(ObjectPath("routines", "sync")).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_EachFileContainsOnlyItsObject()
    {
        // Arrange
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(ObjectPath("views", "active"))).Schemas.Single().Views.Single().Name.ShouldBe("active");
        (await ReadSchema(ObjectPath("routines", "calc"))).Schemas.Single().Routines.Single().Name.ShouldBe("calc");
        (await ReadSchema(ObjectPath("routines", "sync"))).Schemas.Single().Routines.Single().Name.ShouldBe("sync");

        // Act
        // An object file carries nothing but its own object.
        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single();

        // Assert
        users.Views.ShouldBeEmpty();
        users.Routines.ShouldBeEmpty();
        users.Enums.ShouldBeEmpty();
        users.Sequences.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_WritesLeftoverObjectsToPerSchemaHeaderFile()
    {
        // Arrange
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        var header = (await ReadSchema(HeaderPath)).Schemas.Single();

        // Assert
        header.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        header.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
        // The major objects live in their own files, not the header.
        header.Tables.ShouldBeEmpty();
        header.Views.ShouldBeEmpty();
        header.Routines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_AllFilesCombineWithoutDuplicates()
    {
        // Arrange
        // Loading every emitted file together (as desired-schema providers do) must reconstruct the
        // original schema without tripping the aggregator's duplicate detection.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        var app = (await ReadAll()).Schemas.Single();

        // Assert
        app.Tables.Select(t => t.Name).ShouldBe(["users", "orders"], ignoreOrder: true);
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
        app.Routines.Select(r => r.Name).ShouldBe(["calc", "sync"], ignoreOrder: true);
        app.Enums.ShouldHaveSingleItem().Name.ShouldBe("status");
        app.Sequences.ShouldHaveSingleItem().Name.ShouldBe("order_id");
    }

    [Fact]
    public async Task Execute_MultipleSchemas_EachGetsItsOwnDirectory()
    {
        Source(new Database
        {
            Schemas = [
            new Schema { Name = "app", Tables = [MakeTable("users")] },
            new Schema { Name = "audit", Tables = [MakeTable("logs")] },
        ],
        });

        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadSchema(Path.Combine(_dir, "app", "tables", "users.nsql"))).Schemas.Single().Tables.Single().Name.ShouldBe("users");
        (await ReadSchema(Path.Combine(_dir, "audit", "tables", "logs.nsql"))).Schemas.Single().Tables.Single().Name.ShouldBe("logs");
    }

    [Fact]
    public async Task Execute_ObjectFilesCarryNoSchemaStatement()
    {
        // Only the header declares the schema; object files hold just their object, so the declaration
        // doesn't repeat across every file.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        foreach (var type in new[] { "tables", "views", "routines" })
        {
            foreach (var file in ProjectFiles(Path.Combine(_dir, "app", type)))
            {
                var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
                text.ShouldNotContain("CREATE SCHEMA", customMessage: file);
            }
        }
        (await File.ReadAllTextAsync(HeaderPath, TestContext.Current.CancellationToken)).ShouldContain("CREATE SCHEMA app;");
    }

    // ── Additive re-import (merge) ───────────────────────────────────────────

    [Fact]
    public async Task Execute_ReimportPreservesObjectsNotInIncoming()
    {
        // Each object is its own file, so an object absent from a later import is simply not rewritten.
        Source(new Database { Schemas = [new Schema { Name = "app", Tables = [MakeTable("audit_log")] }] });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new Database { Schemas = [new Schema { Name = "app", Tables = [MakeTable("users")] }] });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        (await ReadAll()).Schemas.Single().Tables.Select(t => t.Name).ShouldBe(["audit_log", "users"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_ReimportReplacesAnObjectInPlace()
    {
        // Arrange
        Source(new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "users", Columns = [new Column { Name = "old_col", Type = SqlType.Text }] }] }],
        });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "users", Columns = [new Column { Name = "new_col", Type = SqlType.Text }] }] }],
        });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        var users = (await ReadSchema(ObjectPath("tables", "users"))).Schemas.Single().Tables.Single(t => t.Name.Value.Equals("users"));

        // Assert
        users.Columns.Select(c => c.Name).ShouldBe(["new_col"]);
    }

    [Fact]
    public async Task Execute_ReimportReplacesHeaderObjects()
    {
        // Arrange
        // The header file holds schema-level objects (enums, sequences, domains); a re-import must replace (not
        // duplicate) them.
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Enums = [new EnumType { Name = "status", Values = ["a"] }],
            Sequences = [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1) }],
            Domains = [new DomainType { Name = "typeid", DataType = SqlType.Text }] }],
        };

        Source(schema);
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new Database
        {
            Schemas = [new Schema { Name = "app",
            Enums = [new EnumType { Name = "status", Values = ["a", "b"] }],
            Sequences = [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 100) }],
            Domains = [new DomainType { Name = "typeid", DataType = SqlType.VarChar(64) }] }],
        });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        var header = (await ReadSchema(HeaderPath)).Schemas.Single();

        // Assert
        header.Enums.ShouldHaveSingleItem().Values.ShouldBe(["a", "b"]); // incoming wins
        header.Sequences.ShouldHaveSingleItem().Options.StartWith.ShouldBe(100);
        header.Domains.ShouldHaveSingleItem().DataType.ShouldBe(SqlType.VarChar(64)); // incoming wins, no duplicate
    }

    // ── Extensions (database-global, root-level) ─────────────────────────────

    [Fact]
    public async Task Execute_WritesExtensionsToTopLevelFile()
    {
        // Arrange
        Source(new Database
        {
            Schemas = [new Schema { Name = "app", Tables = [MakeTable("users")] }],
            Extensions = [new Extension { Name = "citext" }, new Extension { Name = "postgis", Version = "3.4" }],
        });

        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        // Extensions land in a single top-level file, not under any per-schema directory.
        var extensions = (await ReadSchema(Path.Combine(_dir, "extensions.nsql"))).Extensions;

        // Assert
        extensions.Select(e => e.Name).ShouldBe(["citext", "postgis"], ignoreOrder: true);
    }

    [Fact]
    public async Task Execute_NoExtensions_WritesNoExtensionsFile()
    {
        await Execute(new ImportArguments { OutputDirectory = _dir });

        File.Exists(Path.Combine(_dir, "extensions.nsql")).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ReimportMergesExtensionsAdditively()
    {
        // Arrange
        Source(new Database
        {
            Schemas = [new Schema { Name = "app", Tables = [MakeTable("users")] }],
            Extensions = [new Extension { Name = "citext" }],
        });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        Source(new Database
        {
            Schemas = [new Schema { Name = "app", Tables = [MakeTable("users")] }],
            Extensions = [new Extension { Name = "postgis", Version = "3.4" }],
        });
        await Execute(new ImportArguments { OutputDirectory = _dir });

        // Act
        var extensions = (await ReadSchema(Path.Combine(_dir, "extensions.nsql"))).Extensions;

        // Assert
        extensions.Select(e => e.Name).ShouldBe(["citext", "postgis"], ignoreOrder: true);
    }

    // ── Canonical layout ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WritesFormatterCanonicalNsql()
    {
        // Arrange
        // Import output must already be in the formatter's canonical layout, so running `fmt` over an
        // imported file changes nothing. This is the invariant that keeps the two DDL paths from drifting.
        Source(RichSchema());

        await Execute(new ImportArguments { OutputDirectory = _dir });

        foreach (var file in ProjectFiles(_dir))
        {

            // Act
            var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);

            // Assert
            NsqlWriter.Format(text).Warnings.ShouldBeEmpty($"{file} is not formatter-canonical");
        }
    }

    private static Database RichSchema() => new Database
    {
        Schemas = [new Schema { Name = "app",
        Tables = [MakeTable("users"), MakeTable("orders")],
        Views = [new View { Name = "active", Body = "SELECT 1" }],
        Routines = [
            new Routine { Name = "calc", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int LANGUAGE sql AS $$ SELECT 1 $$" },
            new Routine { Name = "sync", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "LANGUAGE sql AS $$ SELECT 1 $$" },
        ],
        Enums = [new EnumType { Name = "status", Values = ["a"] }],
        Sequences = [new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1) }] }],
    };
}
