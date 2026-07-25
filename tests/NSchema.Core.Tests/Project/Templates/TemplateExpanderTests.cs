using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Project;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Templates;

public sealed class TemplateExpanderTests
{
    /// <summary>Parses <paramref name="source"/> and assembles it, expanding its templates as the project provider would.</summary>
    private static Database Expand(string source) => Apply(source).Require().Database;

    private static Result<ProjectDefinition> Apply(string source)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return ProjectAssembler.Assemble([result.Value]);
    }

    private static Schema Schema(Database schema, string name)
    {
        var match = schema.Schemas.FirstOrDefault(s => s.Name.Value.Equals(name));
        match.ShouldNotBeNull($"expected a schema named '{name}'");
        return match;
    }

    [Fact]
    public void Expand_InstantiatesTheTemplateIntoEachTargetSchema()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
        Schema(schema, "ordering").Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
    }

    [Fact]
    public void Expand_InstancesCoexistWithHandWrittenObjects()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (id int NOT NULL);
            TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
            APPLY TEMPLATE outbox IN SCHEMA billing;
            """);

        // Assert
        Schema(schema, "billing").Tables.Select(t => t.Name).ShouldBe(["invoices", "outbox"], ignoreOrder: true);
    }

    [Fact]
    public void Expand_RewritesUnqualifiedForeignKeyToTheTargetSchema()
    {
        // Arrange
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE parent (id int NOT NULL, CONSTRAINT pk PRIMARY KEY (id));
              CREATE TABLE child (
                parent_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (parent_id) REFERENCES parent (id)
              );
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Act
        var child = Schema(schema, "billing").Tables.First(t => t.Name.Value.Equals("child"));

        // Assert
        var fk = child.ForeignKeys.ShouldHaveSingleItem();
        fk.References.Schema.ShouldBe("billing");
        fk.References.Name.ShouldBe("parent");
    }

    [Fact]
    public void Expand_LeavesQualifiedForeignKeyAlone()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE child (
                user_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES public.users (id)
              );
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem()
            .References.Schema.ShouldBe("public");
    }

    [Fact]
    public void Expand_QualifiesColumnTypesTheTemplateDeclares()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE ENUM outbox_status ('pending', 'sent');
              CREATE TABLE outbox (status outbox_status NOT NULL, payload text NOT NULL);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        var columns = Schema(schema, "billing").Tables.ShouldHaveSingleItem().Columns;
        // The template-declared enum binds to the instance's schema as a component; the built-in does not.
        columns.First(c => c.Name.Value.Equals("status")).Type.ShouldBe(SqlType.Custom("billing", "outbox_status"));
        columns.First(c => c.Name.Value.Equals("payload")).Type.ShouldBe(SqlType.Text);
    }

    [Fact]
    public void Expand_LeavesTypesTheTemplateDoesNotDeclareAlone()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (kind public.kind_enum NOT NULL, tag citext NOT NULL);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        var columns = Schema(schema, "billing").Tables.ShouldHaveSingleItem().Columns;
        // A qualified type keeps its schema as a component; an unqualified one leaves resolution to the engine.
        columns.First(c => c.Name.Value.Equals("kind")).Type.ShouldBe(SqlType.Custom("public", "kind_enum"));
        columns.First(c => c.Name.Value.Equals("tag")).Type.ShouldBe(SqlType.Custom("citext"));
    }

    [Fact]
    public void Expand_QualifiesCompositeFieldTypesTheTemplateDeclares()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE ENUM status ('a');
              CREATE TYPE envelope AS (state status, note text);
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        var fields = Schema(schema, "billing").CompositeTypes.ShouldHaveSingleItem().Fields;
        fields.First(f => f.Name.Value.Equals("state")).DataType.ShouldBe(SqlType.Custom("billing", "status"));
        fields.First(f => f.Name.Value.Equals("note")).DataType.ShouldBe(SqlType.Text);
    }

    [Fact]
    public void Expand_QualifiesTriggerFunctionTheTemplateDeclares()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE FUNCTION publish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END; $$;
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION publish();
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("billing.publish");
    }

    [Fact]
    public void Expand_LeavesTriggerFunctionTheTemplateDoesNotDeclareAlone()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION public.publish();
            END;
            APPLY TEMPLATE t IN SCHEMA billing;
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("public.publish");
    }

    [Fact]
    public void Expand_UnusedTemplate_IsInert()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA app;
            TEMPLATE unused BEGIN CREATE TABLE x (id int NOT NULL); END;
            """);

        // Assert
        Schema(schema, "app").Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Expand_UnknownTemplate_IsAnError()
        => Apply(
                """
                CREATE SCHEMA app;
                APPLY TEMPLATE missing IN SCHEMA app;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("unknown template 'missing'"));

    [Fact]
    public void Expand_UnknownTargetSchema_IsAnError()
        => Apply(
                """
                TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA missing;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("unknown schema 'missing'"));

    [Fact]
    public void Expand_DuplicateTemplateName_IsAnError()
        => Apply(
                """
                TEMPLATE t BEGIN END;
                TEMPLATE t BEGIN END;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("Duplicate template 't'"));

    [Fact]
    public void Expand_InstanceCollidingWithDeclaredObject_IsAnError()
        => Apply(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.outbox (id int NOT NULL);
                TEMPLATE t BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA billing;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("Table 'billing.outbox' is already declared"));

    [Fact]
    public void Expand_ApplyingTheSameTemplateTwiceToOneSchema_IsAnError()
        => Apply(
                """
                CREATE SCHEMA billing;
                TEMPLATE t BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                APPLY TEMPLATE t IN SCHEMA billing;
                APPLY TEMPLATE t IN SCHEMA billing;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("Table 'billing.outbox' is already declared"));

    // --- table templates (INCLUDE) ---------------------------------------------

    private const string AuditColumns =
        """
        TEMPLATE audit_columns FOR TABLE
        BEGIN
          created_at datetimeoffset NOT NULL,
          updated_at datetimeoffset NOT NULL,
          CONSTRAINT chk_audit CHECK (updated_at >= created_at)
        END;
        """;

    [Fact]
    public void Expand_MergesIncludedMembersIntoTheTable()
    {
        // Act
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE audit_columns);
            {AuditColumns}
            """);

        // Assert
        var table = Schema(schema, "billing").Tables.ShouldHaveSingleItem();
        table.Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at"]);
        table.CheckConstraints.ShouldHaveSingleItem().Name.ShouldBe("chk_audit");
    }

    [Fact]
    public void Expand_InsertsIncludedColumnsAtTheIncludePosition()
    {
        // Act
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (
              id uuid NOT NULL,
              INCLUDE audit_columns,
              total decimal(18,2) NOT NULL
            );
            {AuditColumns}
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem()
            .Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at", "total"]);
    }

    [Fact]
    public void Expand_IncludedForeignKeyBindsToTheIncludingTablesSchema()
    {
        // Arrange
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.tenants (id uuid NOT NULL, CONSTRAINT pk_tenants PRIMARY KEY (id));
            CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE tenant_columns);
            TEMPLATE tenant_columns FOR TABLE
            BEGIN
              tenant_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            END;
            """);

        // Act
        var invoices = Schema(schema, "billing").Tables.First(t => t.Name.Value.Equals("invoices"));

        // Assert
        var fk = invoices.ForeignKeys.ShouldHaveSingleItem();
        fk.References.Schema.ShouldBe("billing");
        fk.References.Name.ShouldBe("tenants");
    }

    [Fact]
    public void Expand_IncludedPrimaryKey_IsSet()
    {
        // Act
        var schema = Expand(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (INCLUDE id_column);
            TEMPLATE id_column FOR TABLE
            BEGIN
              id uuid NOT NULL,
              CONSTRAINT pk_id PRIMARY KEY (id)
            END;
            """);

        // Assert
        Schema(schema, "billing").Tables.ShouldHaveSingleItem().PrimaryKey.ShouldNotBeNull().Name.ShouldBe("pk_id");
    }

    [Fact]
    public void Expand_IncludedPrimaryKeyConflict_IsAnError()
        => Apply(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, CONSTRAINT pk PRIMARY KEY (id), INCLUDE id_column);
                TEMPLATE id_column FOR TABLE
                BEGIN
                  surrogate_id uuid NOT NULL,
                  CONSTRAINT pk_id PRIMARY KEY (surrogate_id)
                END;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("already declares a primary key"));

    [Fact]
    public void Expand_IncludeInsideSchemaTemplateTable_ResolvesPerInstance()
    {
        // Composition: a schema template's table includes a table template; each instantiated copy resolves the
        // include against its own schema.
        var schema = Expand(
            $"""
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL, INCLUDE audit_columns);
            END;
            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            {AuditColumns}
            """);

        foreach (var name in new[] { "billing", "ordering" })
        {
            var outbox = Schema(schema, name).Tables.ShouldHaveSingleItem();
            outbox.Columns.Select(c => c.Name).ShouldBe(["id", "created_at", "updated_at"]);
        }
    }

    [Fact]
    public void Expand_DuplicateColumnFromInclude_IsAnError()
        => Apply(
                $"""
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (created_at datetimeoffset NOT NULL, INCLUDE audit_columns);
                {AuditColumns}
                """)
            .Errors.ShouldContain(d => d.Message.Contains("already declares column 'created_at'"));

    [Fact]
    public void Expand_DuplicateForeignKeyFromInclude_IsRejectedWithoutApplyingTheFragment()
    {
        // Arrange
        var result = Apply(
            """
            CREATE SCHEMA billing;
            CREATE TABLE billing.invoices (
              tenant_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES billing.tenants (id),
              INCLUDE tenant_audit
            );
            TEMPLATE tenant_audit FOR TABLE
            BEGIN
              audit_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (audit_id) REFERENCES billing.audits (id)
            END;
            """);

        // Act
        var table = Schema(result.Require().Database, "billing").Tables.ShouldHaveSingleItem();

        // Assert
        result.Errors.ShouldContain(d => d.Message.Contains("foreign key 'fk_tenant'"));
        table.Columns.Select(c => c.Name).ShouldBe(["tenant_id"]);
        table.ForeignKeys.ShouldHaveSingleItem();
    }

    [Fact]
    public void Expand_UnknownInclude_IsAnError()
        => Apply(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE missing);
                """)
            .Errors.ShouldContain(d => d.Message.Contains("unknown template 'missing'"));

    [Fact]
    public void Expand_IncludingASchemaTemplate_IsAnError()
        => Apply(
                """
                CREATE SCHEMA billing;
                CREATE TABLE billing.invoices (id uuid NOT NULL, INCLUDE outbox);
                TEMPLATE outbox BEGIN CREATE TABLE outbox (id uuid NOT NULL); END;
                """)
            .Errors.ShouldContain(d => d.Message.Contains("only a FOR TABLE template can be included"));

    [Fact]
    public void Expand_ApplyingATableTemplateToSchemas_IsAnError()
        => Apply(
                $"""
                CREATE SCHEMA billing;
                APPLY TEMPLATE audit_columns IN SCHEMA billing;
                {AuditColumns}
                """)
            .Errors.ShouldContain(d => d.Message.Contains("is a table template"));

    [Fact]
    public void Expand_InstantiatesTemplateMigrationsPerAppliedSchema()
    {
        // Act
        var instances = Apply(
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL, trace_id text NOT NULL );
              SCRIPT backfill_trace RUN ON ADD COLUMN outbox_events.trace_id AS $$ UPDATE {schema}.outbox_events SET trace_id = ''; $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """).Require().AllScripts();

        // Assert — one instance per applied schema, with the schema bound and the {schema} token substituted.
        instances.Count.ShouldBe(2);
        instances[0].ShouldBeOfType<ChangeScript>().Path.ShouldBe("sales.outbox_events.trace_id");
        instances[0].Sql.ShouldBe("UPDATE sales.outbox_events SET trace_id = '';");
        instances[0].Name.ShouldBe("backfill_trace");
        instances[1].ShouldBeOfType<ChangeScript>().Path.ShouldBe("billing.outbox_events.trace_id");
        instances[1].Sql.ShouldBe("UPDATE billing.outbox_events SET trace_id = '';");
    }

    [Fact]
    public void Expand_UnappliedTemplate_InstantiatesNoMigrations()
    {
        // Act — a declared-but-never-applied template contributes nothing.
        var instances = Apply(
            """
            CREATE SCHEMA sales;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT backfill RUN ON ADD COLUMN outbox_events.trace_id AS $$ SELECT 1; $$;
            END;
            """).Require().AllScripts();

        // Assert
        instances.ShouldBeEmpty();
    }

    [Fact]
    public void Expand_MigrationWithoutToken_KeepsSqlVerbatim()
    {
        // Act
        var migration = Apply(
            """
            CREATE SCHEMA sales;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT version RUN ON ADD COLUMN outbox_events.trace_id (run_outside_transaction = true) AS $$ SELECT version(); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales;
            """).Require().AllScripts().ShouldHaveSingleItem();

        // Assert — no token, no rewriting; the option carries onto the instance.
        migration.Sql.ShouldBe("SELECT version();");
        migration.RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Expand_InstantiatesTemplateDeploymentScriptsPerAppliedSchema()
    {
        // Act
        var scripts = Apply(
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE outbox
            BEGIN
              CREATE TABLE outbox_events ( id int NOT NULL );
              SCRIPT seed RUN ONCE ON POST DEPLOYMENT AS $$ INSERT INTO {schema}.outbox_events VALUES (1); $$;
            END;
            APPLY TEMPLATE outbox IN SCHEMA sales, billing;
            """).Require().AllScripts();

        // Assert — one instance per applied schema, scoped to it via the event (the shared name is two
        // distinct scripts), token substituted in the body, run condition carried.
        scripts.Count.ShouldBe(2);
        scripts[0].ShouldBeOfType<DeploymentScript>().ScopeSchema.ShouldBe("sales");
        scripts[0].Address.ShouldBe(new ScopedAddress("sales", "seed"));
        scripts[0].Sql.ShouldBe("INSERT INTO sales.outbox_events VALUES (1);");
        scripts[0].ShouldBeOfType<DeploymentScript>().RunCondition.ShouldBe(RunCondition.Once);
        scripts[1].ShouldBeOfType<DeploymentScript>().ScopeSchema.ShouldBe("billing");
        scripts[1].Address.ShouldBe(new ScopedAddress("billing", "seed"));
    }
}
