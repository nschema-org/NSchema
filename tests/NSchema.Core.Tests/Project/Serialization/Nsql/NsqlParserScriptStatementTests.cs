using NSchema.Model.Scripts;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Covers the unified <c>SCRIPT &lt;name&gt; RUN [ALWAYS | ONCE] ON &lt;event&gt; AS $$ … $$;</c> statement —
/// the only form of deployment scripts and data migrations — and the pointed errors raised for the
/// pre-5.0 spellings.
/// </summary>
public sealed class NsqlParserScriptStatementTests
{
    private static ProjectDefinition Read(string source) => new TestNsqlParser(source).Parse();

    private static IReadOnlyList<ChangeScript> Migrations(ProjectDefinition document) =>
        document.Directives.ChangeScripts;

    // -------------------------------------------------------------------------
    // Deployment events
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_PreDeploymentEvent_ProducesAScript()
    {
        // Act
        var script = Read("SCRIPT enable_citext RUN ON PRE DEPLOYMENT AS $$ CREATE EXTENSION IF NOT EXISTS citext; $$;")

        // Assert
            .Directives.DeploymentScripts.ShouldHaveSingleItem();

        script.Name.ShouldBe("enable_citext");
        script.Phase.ShouldBe(DeploymentPhase.Pre);
        script.Sql.ShouldBe("CREATE EXTENSION IF NOT EXISTS citext;");
        script.RunCondition.ShouldBe(RunCondition.Always);
    }

    [Fact]
    public void Parse_PostDeploymentEvent_ProducesAScript()
        => Read("SCRIPT reindex RUN ON POST DEPLOYMENT AS $$ SELECT 1; $$;")
            .Directives.DeploymentScripts.ShouldHaveSingleItem().Phase.ShouldBe(DeploymentPhase.Post);

    [Fact]
    public void Parse_RunAlways_IsTheExplicitSpellingOfTheDefault()
        => Read("SCRIPT x RUN ALWAYS ON PRE DEPLOYMENT AS $$ SELECT 1; $$;")
            .Directives.DeploymentScripts.ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Always);

    [Fact]
    public void Parse_RunOnce_SetsTheRunCondition()
        => Read("SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO app.currencies VALUES ('GBP'); $$;")
            .Directives.DeploymentScripts.ShouldHaveSingleItem().RunCondition.ShouldBe(RunCondition.Once);

    [Fact]
    public void Parse_Options_AreSharedWithTheOldForm()
        => Read("SCRIPT x RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$ SELECT 1; $$;")
            .Directives.DeploymentScripts.ShouldHaveSingleItem().RunOutsideTransaction.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Change events
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_AddColumnEvent_ProducesADataMigration()
    {
        // Act
        var migration = Migrations(Read("SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$ UPDATE app.users SET email = ''; $$;"))

        // Assert
            .ShouldHaveSingleItem();

        migration.Name.ShouldBe("backfill_emails");
        migration.Target.Trigger.ShouldBe(ChangeTrigger.AddColumn);
        migration.Path.ShouldBe("app.users.email");
    }

    [Theory]
    [InlineData("ALTER COLUMN TYPE app.orders.total", ChangeTrigger.AlterColumnType)]
    [InlineData("ADD CONSTRAINT app.orders.total_positive", ChangeTrigger.AddConstraint)]
    public void Parse_OtherChangeEvents_CarryTheTrigger(string eventText, ChangeTrigger trigger)
        => Migrations(Read($"SCRIPT x RUN ON {eventText} AS $$ SELECT 1; $$;"))
            .ShouldHaveSingleItem().Target.Trigger.ShouldBe(trigger);

    [Fact]
    public void Parse_RunConditionOnChangeEvent_IsRejected()
        // A change-event script runs whenever its change is planned, so it takes a bare RUN — a condition is
        // only valid on a deployment event.
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT x RUN ONCE ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("only valid on a deployment event");

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_UnlessExists_IsReserved()
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT x RUN UNLESS EXISTS (SELECT 1) ON POST DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("'UNLESS EXISTS' is reserved for a future release");

    [Fact]
    public void Parse_QuotedName_NoLongerParses()
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT 'x' RUN ON PRE DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected a script name");

    [Fact]
    public void Parse_MissingRun_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT x ON PRE DEPLOYMENT AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'RUN'");

    [Fact]
    public void Parse_MissingOn_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT x RUN AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected 'ON'");

    [Fact]
    public void Parse_UnknownEvent_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Read("SCRIPT x RUN ON DELETE TABLE app.users AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Expected a script event");

    // -------------------------------------------------------------------------
    // Removed pre-5.0 spellings
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_OldDeploymentForm_NoLongerParses()
        => Should.Throw<NsqlSyntaxException>(() => Read("POST DEPLOYMENT 'reindex' AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Unknown statement 'POST'");

    [Fact]
    public void Parse_OldMigrationForm_NoLongerParses()
        => Should.Throw<NsqlSyntaxException>(() => Read("MIGRATION 'backfill' FOR ADD COLUMN app.users.email AS $$ SELECT 1; $$;"))
            .Message.ShouldContain("Unknown statement 'MIGRATION'");

    // -------------------------------------------------------------------------
    // Template bodies
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ChangeEventScriptInTemplate_BindsTheSchemaPerApplication()
    {
        // Arrange
        var read = NsqlReader.Read(
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT backfill_trace RUN ON ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);
        read.IsSuccess.ShouldBeTrue();
        var assembled = NSchema.Project.ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue();

        // Act
        var changes = assembled.Value.AllScripts().OfType<ChangeScript>().ToList();

        // Assert
        changes.Select(c => c.ScopeSchema!.Value).ShouldBe(["billing", "ordering"]);
        changes.ShouldAllBe(c => c.Target.Table == "outbox_events");
        changes.ShouldAllBe(c => c.Target.Member == "trace_id");
    }

    [Fact]
    public void Parse_DeploymentScriptInTemplate_InstantiatesPerAppliedSchema()
    {
        // Arrange
        var read = NsqlReader.Read(
            """
            CREATE SCHEMA app;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO {schema}.outbox_events VALUES (1); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA app;
            """);
        read.IsSuccess.ShouldBeTrue();
        var assembled = NSchema.Project.ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue();

        var script = assembled.Value.AllScripts().ShouldHaveSingleItem();
        script.Name.ShouldBe("seed");
        script.Sql.Value.ShouldContain("INSERT INTO app.outbox_events");

        // Act
        var deployment = script.ShouldBeOfType<DeploymentScript>();

        // Assert
        deployment.Phase.ShouldBe(DeploymentPhase.Post);
        deployment.ScopeSchema.ShouldBe("app");
        deployment.RunCondition.ShouldBe(RunCondition.Once);
    }

    [Fact]
    public void Parse_OldMigrationFormInTemplate_IsRejected()
        => Should.Throw<NsqlSyntaxException>(() => Read(
            """
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              MIGRATION FOR ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            """)).Message.ShouldContain("Unexpected 'MIGRATION' inside a template");
}
