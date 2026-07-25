using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

public sealed class ProjectProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-desired-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relativePath, string sql)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sql);
    }

    private static ProjectSource Source(string root, string glob)
    {
        var matcher = new Matcher();
        matcher.AddInclude(glob);
        return new ProjectSource(root, matcher);
    }

    private static ScopedAddress Scoped(string schema, string name) =>
        new(schema, name);

    [Fact]
    public async Task GetProject_NoSources_Throws()
        => await Should.ThrowAsync<InvalidOperationException>(() => new ProjectProvider([]).GetProject(PlanningScope.All).AsTask());

    [Fact]
    public async Task GetProject_NoMatchingFiles_FailsTheRead()
    {
        // Arrange
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        project.IsFailure.ShouldBeTrue();
        project.Errors.ShouldHaveSingleItem().ShouldBe(ProjectDiagnostics.NoFilesMatched());
    }

    [Fact]
    public async Task GetProject_SyntaxError_FailsTheRead_AndNamesTheFile()
    {
        // Arrange
        Write("good.sql", "CREATE SCHEMA app;");
        Write("bad.sql", "CREATE TABLE app.users (");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        // The read fails, naming the broken file structurally — while the readable files still aggregate
        // into the carried project.
        project.IsFailure.ShouldBeTrue();
        var error = project.Errors.ShouldHaveSingleItem().ShouldBeOfType<NsqlDiagnostic>();
        error.Source.ShouldBe("syntax");
        error.File.ShouldBe(Path.Combine(_root, "bad.sql"));
        error.Position.Line.ShouldBe(1);
        project.Value!.Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task GetProject_AggregatesAllSources()
    {
        // Arrange
        Write("a.sql", "CREATE SCHEMA a;");
        Write("b.sql", "CREATE SCHEMA b;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.Database.Schemas.Select(s => s.Name).ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProject_LayersMultipleSources()
    {
        // Arrange
        // Mirrors the CLI's base + environment-overlay registration: two sources aggregate.
        Write("base.sql", "CREATE SCHEMA app;");
        Write("overlay.sql", "CREATE SCHEMA audit;");
        var sut = new ProjectProvider([Source(_root, "base.sql"), Source(_root, "overlay.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.Database.Schemas.Select(s => s.Name).ShouldBe(["app", "audit"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProject_SurfacesDeploymentScripts()
    {
        // Arrange
        Write("schema.sql",
            """
            CREATE SCHEMA app;
            SCRIPT backfill RUN ON POST DEPLOYMENT AS $$ UPDATE app.t SET x = 1; $$;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.AllScripts().ShouldHaveSingleItem().Name.ShouldBe("backfill");
    }

    [Fact]
    public async Task GetProject_FiltersSchemaByScope()
    {
        Write("schema.sql", "CREATE SCHEMA app; CREATE SCHEMA audit;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        var project = (await sut.GetProject(PlanningScope.To(new SchemaAddress("app")), TestContext.Current.CancellationToken)).Value!;

        project.Database.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task GetProject_AggregatesMigrationsAcrossFiles()
    {
        // Arrange
        // Two files, and a same-path pair distinguished only by trigger — all three aggregate (no false duplicate).
        Write("a.sql",
            """
            CREATE SCHEMA app;
            SCRIPT backfill RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            SCRIPT retype RUN ON ALTER COLUMN TYPE app.users.email AS $$ SELECT 2; $$;
            """);
        Write("b.sql", "SCRIPT guard RUN ON ADD CONSTRAINT app.orders.total_positive AS $$ SELECT 3; $$;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.AllScripts().Select(m => m.Name).ShouldBe(["backfill", "retype", "guard"]);
    }

    [Fact]
    public async Task GetProject_DuplicateMigrationAcrossFiles_FailsTheRead()
    {
        // Arrange
        Write("a.sql",
            """
            CREATE SCHEMA app;
            SCRIPT first RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            """);
        Write("b.sql", "SCRIPT other RUN ON ADD COLUMN app.users.email AS $$ SELECT 2; $$;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Duplicate migration"));
    }

    [Fact]
    public async Task GetProject_CaseVariantMigrationTargets_AreDistinct()
    {
        // Arrange
        // Identifiers are case-sensitive, so scripts addressing case-variant paths target different members
        // and are not duplicates of one another.
        Write("a.sql",
            """
            CREATE SCHEMA app;
            SCRIPT lower RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            """);
        Write("b.sql", "SCRIPT upper RUN ON ADD COLUMN APP.Users.EMAIL AS $$ SELECT 2; $$;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.Errors.ShouldNotContain(d => d.Message.Contains("Duplicate migration"));
        result.Value!.Directives.ChangeScripts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetProject_ScopeFilter_DropsOutOfScopeMigrations()
    {
        // A scoped run can never match a migration targeting an unplanned schema, so it is dropped;
        // in-scope migrations survive.
        Write("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE SCHEMA audit;
            SCRIPT app_backfill RUN ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;
            SCRIPT audit_backfill RUN ON ADD COLUMN audit.log.detail AS $$ SELECT 2; $$;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        var project = (await sut.GetProject(PlanningScope.To(new SchemaAddress("app")), TestContext.Current.CancellationToken)).Value!;

        project.AllScripts().ShouldHaveSingleItem().ShouldBeOfType<ChangeScript>().ScopeSchema.ShouldBe("app");
    }

    [Fact]
    public async Task GetProject_ExpandsTemplatesAcrossFiles()
    {
        // Arrange
        // Templates are location-agnostic: the definition, its application, and the target schemas may each live
        // in a different file — expansion runs on the aggregate.
        Write("templates.sql", "TEMPLATE outbox BEGIN CREATE TABLE outbox (id int NOT NULL); END;");
        Write("schemas.sql",
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.Database.Schemas.Select(s => s.Name).ShouldBe(["billing", "ordering"], ignoreOrder: true);
        project.Database.Schemas.ShouldAllBe(s => s.Tables.Count == 1);
    }

    [Fact]
    public async Task GetProject_ResolvesTableTemplateIncludesAcrossFiles()
    {
        // Arrange
        Write("templates.sql",
            """
            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at datetimeoffset NOT NULL
            END;
            """);
        Write("schema.sql",
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE audit_columns);
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert
        var table = project.Database.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.Columns.Select(c => c.Name).ShouldBe(["id", "created_at"]);
    }

    [Fact]
    public async Task GetProject_TemplateInstancesRespectScopeFilter()
    {
        // Expansion happens before the scope filter, so an instance in an out-of-scope schema is filtered away.
        Write("schema.sql",
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id int NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        var project = (await sut.GetProject(PlanningScope.To(new SchemaAddress("billing")), TestContext.Current.CancellationToken)).Value!;

        project.Database.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
    }

    [Fact]
    public async Task GetProject_TemplateMigrations_InstantiatePerAppliedSchema()
    {
        // Arrange
        Write("schema.sql", "CREATE SCHEMA sales; CREATE SCHEMA billing;");
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT backfill_trace RUN ON ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert — the {schema} token substitutes in the body; the instances keep the declared name and are
        // kept distinct by their scope.
        project.AllScripts().Count.ShouldBe(2);
        project.AllScripts().Cast<ChangeScript>().Select(m => m.Path).ShouldBe(["sales.outbox_events.trace_id", "billing.outbox_events.trace_id"]);
        project.AllScripts().Select(m => m.Address).ShouldBe([Scoped("sales", "backfill_trace"), Scoped("billing", "backfill_trace")]);
        project.AllScripts()[1].Sql.ShouldBe("UPDATE billing.outbox_events SET trace_id = '';");
    }

    [Fact]
    public async Task GetProject_TemplateMigrationInstances_AreDistinctPerSchema()
    {
        // Arrange — applying to two schemas instantiates the block twice; the instances share a name and are
        // distinct scripts, because identity is (scope, name) and each scopes to its applied schema.
        Write("schema.sql", "CREATE SCHEMA sales; CREATE SCHEMA billing;");
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT backfill_trace RUN ON ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.AllScripts().Select(s => s.Address).ShouldBe([Scoped("sales", "backfill_trace"), Scoped("billing", "backfill_trace")]);
    }

    [Fact]
    public async Task GetProject_TemplateMigrationCollidingWithHandWrittenBlock_FailsTheRead()
    {
        // Arrange — an instantiated block lands in the same pool as hand-written ones, so a collision is the
        // ordinary duplicate error.
        Write("schema.sql",
            """
            CREATE SCHEMA sales;
            SCRIPT handwritten RUN ON ADD COLUMN sales.outbox_events.trace_id AS $$ SELECT 1; $$;
            """);
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT templated RUN ON ADD COLUMN outbox_events.trace_id AS $$ SELECT 2; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act / Assert
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Duplicate migration"));
    }

    [Fact]
    public async Task GetProject_DuplicateScriptNames_FailTheRead()
    {
        // Arrange — the address identifies a script (run-once tracking, diagnostics), so two globals sharing a
        // name is a collision, whichever statement forms are involved.
        Write("a.sql", "SCRIPT seed RUN ON POST DEPLOYMENT AS $$ SELECT 1; $$;");
        Write("b.sql", "SCRIPT seed RUN ON POST DEPLOYMENT AS $$ SELECT 2; $$;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act / Assert
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DuplicateScriptName(new ScopedAddress(null, "seed")));
    }

    [Fact]
    public async Task GetProject_DuplicateScriptNamesInTheSameScope_FailTheRead()
    {
        // Arrange — two change-event scripts in the same schema sharing a name collide; the diagnostic renders
        // the scoped address.
        Write("schema.sql",
            """
            CREATE SCHEMA sales;
            SCRIPT seed RUN ON ADD COLUMN sales.orders.total AS $$ SELECT 1; $$;
            SCRIPT seed RUN ON ADD COLUMN sales.orders.tax AS $$ SELECT 2; $$;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act / Assert
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem()
            .ShouldBe(ProjectDiagnostics.DuplicateScriptName(new ScopedAddress("sales", "seed")));
    }

    [Fact]
    public async Task GetProject_ScriptStatements_ProduceNoDiagnostics()
    {
        // Arrange
        Write("scripts.sql", "SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ SELECT 1; $$;");
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var result = await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.Value!.AllScripts().ShouldHaveSingleItem().ShouldBeOfType<DeploymentScript>().RunCondition.ShouldBe(RunCondition.Once);
    }

    [Fact]
    public async Task GetProject_TemplateDeploymentScripts_InstantiatePerAppliedSchema()
    {
        // Arrange
        Write("schema.sql", "CREATE SCHEMA sales; CREATE SCHEMA billing;");
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO {schema}.outbox_events VALUES (1); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.All, TestContext.Current.CancellationToken)).Value!;

        // Assert — each instance keeps the declared name and scopes to its applied schema.
        project.AllScripts().Select(s => s.Address).ShouldBe([Scoped("sales", "seed"), Scoped("billing", "seed")]);
    }

    [Fact]
    public async Task GetProject_TemplateDeploymentScripts_RespectTheSchemaScopeFilter()
    {
        // Arrange — a scoped run drops instances the templates created for out-of-scope schemas, while
        // hand-written deployment scripts are global and always survive.
        Write("schema.sql",
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            SCRIPT global RUN ON PRE DEPLOYMENT AS $$ SELECT 1; $$;
            """);
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT seed RUN ON POST DEPLOYMENT AS $$ INSERT INTO {schema}.outbox_events VALUES (1); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.To(new SchemaAddress("billing")), TestContext.Current.CancellationToken)).Value!;

        // Assert
        project.AllScripts().Select(s => s.Address).ShouldBe([new ScopedAddress(null, "global"), Scoped("billing", "seed")]);
    }

    [Fact]
    public async Task GetProject_TemplateMigrations_RespectTheSchemaScopeFilter()
    {
        // Arrange
        Write("schema.sql", "CREATE SCHEMA sales; CREATE SCHEMA billing;");
        Write("outbox.sql",
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT backfill RUN ON ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """);
        var sut = new ProjectProvider([Source(_root, "**/*.sql")]);

        // Act
        var project = (await sut.GetProject(PlanningScope.To(new SchemaAddress("billing")), TestContext.Current.CancellationToken)).Value!;

        // Assert — only the in-scope instance survives.
        project.AllScripts().ShouldHaveSingleItem().ShouldBeOfType<ChangeScript>().Path.ShouldBe("billing.outbox_events.trace_id");
    }
}
