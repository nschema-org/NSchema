using NSchema.Model.Schemas;
using NSchema.Project;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Template statements: the syntax shapes they parse to, the read-time validation of their bodies, and the
/// projection semantics their instances carry (bodies stay unexpanded in the tree; expansion is covered in
/// depth by <c>TemplateExpanderTests</c>).
/// </summary>
public sealed class NsqlParserTemplateTests
{
    private static IReadOnlyList<NSchema.Project.Nsql.Syntax.NsqlStatement> Statements(string source)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Statements;
    }

    /// <summary>Assembles a template plus an application into schema <c>app</c>, returning the instance.</summary>
    private static Schema ExpandIntoApp(string templateSource)
    {
        var source = $"CREATE SCHEMA app;\n{templateSource}\nAPPLY TEMPLATE t IN SCHEMA app;";
        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        var assembled = ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue();
        return assembled.Value.Database.Schemas.ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_Template_BodyStaysUnexpandedInTheTree()
    {
        // Act
        var template = Statements(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL);
              CREATE SEQUENCE outbox_seq;
            END;
            """).ShouldHaveSingleItem().ShouldBeOfType<SchemaTemplateStatement>();

        // Assert
        template.Name.Value.ShouldBe("t");
        template.Statements.Count.ShouldBe(2);
        template.Statements[0].ShouldBeOfType<CreateTableStatement>().Name.Schema.ShouldBeNull();
    }

    [Fact]
    public void Expand_Template_InstantiatesObjectsIntoTheTargetSchema()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL);
              CREATE SEQUENCE outbox_seq;
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("outbox");
        app.Sequences.ShouldHaveSingleItem().Name.ShouldBe("outbox_seq");
    }

    [Fact]
    public void Parse_Template_ObjectsAreNotPartOfTheSchema()
        => new TestNsqlParser("TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;").Parse()
            .Database.Schemas.ShouldBeEmpty();

    [Fact]
    public void Expand_Template_UnqualifiedForeignKeyBindsToTheAppliedSchema()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE parent (id int NOT NULL);
              CREATE TABLE child (
                user_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES parent (id)
              );
            END;
            """);

        // Assert
        app.Tables.Single(t => t.Name == "child").ForeignKeys.ShouldHaveSingleItem()
            .References.Schema.ShouldBe("app");
    }

    [Fact]
    public void Expand_Template_QualifiedForeignKeyEscapes()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE child (
                user_id int NOT NULL,
                CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES public.users (id)
              );
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem()
            .References.Schema.ShouldBe("public");
    }

    [Fact]
    public void Expand_Template_StandaloneIndexAttachesToItsTable()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (created_at datetimeoffset NOT NULL);
              CREATE INDEX ix_outbox_created_at ON outbox (created_at);
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem()
            .Name.ShouldBe("ix_outbox_created_at");
    }

    [Fact]
    public void Expand_Template_TriggerAttachesToItsTable()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              CREATE TRIGGER trg AFTER INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION publish();
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem()
            .Function.ShouldBe("publish");
    }

    [Fact]
    public void Expand_Template_TableGrantAttaches()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id int NOT NULL);
              GRANT SELECT, INSERT ON outbox TO svc;
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Grants.ShouldHaveSingleItem().Role.ShouldBe("svc");
    }

    [Fact]
    public void Expand_Template_DocCommentAttachesToInnerObject()
    {
        // Act
        var app = ExpandIntoApp(
            """
            TEMPLATE t
            BEGIN
              --- The transactional outbox.
              CREATE TABLE outbox (id int NOT NULL);
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Comment.ShouldBe("The transactional outbox.");
    }

    [Fact]
    public void Expand_Template_EmptyBody_AddsNothing()
        => ExpandIntoApp("TEMPLATE t BEGIN END;").Tables.ShouldBeEmpty();

    [Fact]
    public void Parse_TemplateAndSchema_Coexist()
    {
        // Act
        var statements = Statements(
            """
            CREATE SCHEMA app;
            TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL); END;
            APPLY TEMPLATE t IN SCHEMA app;
            """);

        // Assert
        statements.Count.ShouldBe(3);
        statements[0].ShouldBeOfType<CreateSchemaStatement>().Name.Value.ShouldBe("app");
        statements[1].ShouldBeOfType<SchemaTemplateStatement>().Name.Value.ShouldBe("t");
        statements[2].ShouldBeOfType<ApplyTemplateStatement>().TemplateName.Value.ShouldBe("t");
    }

    [Fact]
    public void Parse_ApplyTemplate_CapturesNameAndSchemaList()
    {
        // Act
        var application = Statements("APPLY TEMPLATE outbox IN SCHEMA billing, ordering, shipping;")

        // Assert
            .ShouldHaveSingleItem().ShouldBeOfType<ApplyTemplateStatement>();

        application.TemplateName.Value.ShouldBe("outbox");
        application.Schemas.Select(s => s.Value).ShouldBe(["billing", "ordering", "shipping"]);
    }

    [Fact]
    public void Parse_ApplyTemplate_DuplicateSchema_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("APPLY TEMPLATE t IN SCHEMA a, b, a;").Parse())
            .Message.ShouldContain("more than once");

    [Fact]
    public void Parse_ApplyTemplate_CaseVariantSchemas_AreDistinct()
        => Should.NotThrow(() => new TestNsqlParser("APPLY TEMPLATE t IN SCHEMA a, A;").Parse());

    [Fact]
    public void Parse_Template_QualifiedDeclaration_FailsTheRead()
        => new TestNsqlParser(
                "TEMPLATE t BEGIN CREATE TABLE app.outbox (id int NOT NULL); END;").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("unqualified");

    [Fact]
    public void Parse_Template_CreateSchemaInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN CREATE SCHEMA app; END;").Parse())
            .Message.ShouldContain("CREATE SCHEMA is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateViewInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN CREATE VIEW v AS SELECT 1; END;").Parse())
            .Message.ShouldContain("CREATE VIEW is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateMaterializedViewInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN CREATE MATERIALIZED VIEW v AS SELECT 1; END;").Parse())
            .Message.ShouldContain("CREATE MATERIALIZED VIEW is not supported inside a template");

    [Fact]
    public void Parse_Template_CreateExtensionInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN CREATE EXTENSION citext; END;").Parse())
            .Message.ShouldContain("CREATE EXTENSION is not supported inside a template");

    [Fact]
    public void Parse_Template_GrantUsageOnSchemaInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN GRANT USAGE ON SCHEMA app TO svc; END;").Parse())
            .Message.ShouldContain("GRANT USAGE ON SCHEMA is not supported inside a template");

    [Fact]
    public void Parse_Template_DropInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN DROP TABLE x; END;").Parse())
            .Message.ShouldContain("Unexpected 'DROP' inside a template");

    [Fact]
    public void Parse_Template_OldDeploymentFormInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN PRE DEPLOYMENT 'x' AS $$ SELECT 1; $$; END;").Parse())
            .Message.ShouldContain("inside a template");

    [Fact]
    public void Parse_Template_NestedTemplateInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN TEMPLATE u BEGIN END; END;").Parse())
            .Message.ShouldContain("inside a template");

    [Fact]
    public void Parse_Template_Unterminated_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t BEGIN CREATE TABLE x (id int NOT NULL);").Parse())
            .Message.ShouldContain("Unterminated template 't'");

    [Fact]
    public void Parse_Template_MissingBegin_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t CREATE TABLE x (id int NOT NULL); END;").Parse())
            .Message.ShouldContain("BEGIN");

    [Fact]
    public void Parse_UnqualifiedNameOutsideTemplate_StillThrows()
        // The template-body binding must not leak: outside a template every name stays schema-qualified.
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE TABLE outbox (id int NOT NULL);").Parse())
            .Message.ShouldContain("Expected '.'");

    // --- table templates (FOR TABLE) and INCLUDE members -----------------------

    [Fact]
    public void Parse_TableTemplate_CapturesMembers()
    {
        // Act
        var template = Statements(
            """
            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at datetimeoffset NOT NULL,
              updated_at datetimeoffset NOT NULL,
              CONSTRAINT chk_updated CHECK (updated_at >= created_at),
              INDEX ix_updated_at (updated_at)
            END;
            """).ShouldHaveSingleItem().ShouldBeOfType<TableTemplateStatement>();

        // Assert
        template.Members.Count.ShouldBe(4);
        template.Members.OfType<ColumnDefinition>().Select(c => c.Name.Value).ShouldBe(["created_at", "updated_at"]);
        template.Members.OfType<NSchema.Project.Nsql.Syntax.Constraints.CheckDefinition>().ShouldHaveSingleItem().Name.Value.ShouldBe("chk_updated");
        template.Members.OfType<NSchema.Project.Nsql.Syntax.Indexes.IndexDefinition>().ShouldHaveSingleItem().Name.Value.ShouldBe("ix_updated_at");
    }

    [Fact]
    public void Parse_TemplateWithoutFor_IsASchemaTemplate()
        => Statements("TEMPLATE t BEGIN END;").ShouldHaveSingleItem().ShouldBeOfType<SchemaTemplateStatement>();

    [Fact]
    public void Parse_ExplicitForSchema_IsAccepted()
        => Statements("TEMPLATE t FOR SCHEMA BEGIN END;").ShouldHaveSingleItem().ShouldBeOfType<SchemaTemplateStatement>();

    [Fact]
    public void Parse_ForUnknownKind_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("TEMPLATE t FOR VIEW BEGIN END;").Parse())
            .Message.ShouldContain("Expected SCHEMA or TABLE after FOR");

    [Fact]
    public void Expand_TableTemplate_UnqualifiedForeignKeyBindsToTheIncludingTablesSchema()
    {
        // Arrange
        var source =
            """
            CREATE SCHEMA app;
            CREATE TABLE app.tenants (id uuid NOT NULL);
            TEMPLATE tenant_columns FOR TABLE
            BEGIN
              tenant_id uuid NOT NULL,
              CONSTRAINT fk_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            END;
            CREATE TABLE app.orders (id int NOT NULL, INCLUDE tenant_columns);
            """;
        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        var assembled = ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue();

        var orders = assembled.Value.Database.Schemas.ShouldHaveSingleItem()

            // Act
            .Tables.Single(t => t.Name == "orders");

        // Assert
        orders.ForeignKeys.ShouldHaveSingleItem().References.Schema.ShouldBe("app");
    }

    [Fact]
    public void Parse_TableTemplate_EmptyBody_YieldsNoMembers()
        => Statements("TEMPLATE t FOR TABLE BEGIN END;").ShouldHaveSingleItem()
            .ShouldBeOfType<TableTemplateStatement>().Members.ShouldBeEmpty();

    [Fact]
    public void Parse_TableTemplate_IncludeInside_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser(
                """
                TEMPLATE t FOR TABLE
                BEGIN
                  INCLUDE other_template,
                  created_at datetimeoffset NOT NULL
                END;
                """).Parse())
            .Message.ShouldContain("cannot include another");

    [Fact]
    public void Parse_Include_IsAMemberAtItsWrittenPosition()
    {
        // Act
        var table = Statements(
            """
            CREATE TABLE app.invoices (
              id uuid NOT NULL,
              INCLUDE audit_columns,
              total decimal(18,2) NOT NULL
            );
            """).ShouldHaveSingleItem().ShouldBeOfType<CreateTableStatement>();

        // Assert
        table.Members.Count.ShouldBe(3);
        table.Members[1].ShouldBeOfType<IncludeMember>().TemplateName.Value.ShouldBe("audit_columns");
    }

    [Fact]
    public void Parse_Include_AsLastMember()
        => Statements("CREATE TABLE app.t (id int NOT NULL, INCLUDE audit_columns);")
            .ShouldHaveSingleItem().ShouldBeOfType<CreateTableStatement>()
            .Members[1].ShouldBeOfType<IncludeMember>().TemplateName.Value.ShouldBe("audit_columns");

    [Fact]
    public void Parse_ColumnNamedInclude_StillParsesAsAColumn()
    {
        // Compat: 'include' followed by anything more than a bare identifier is a column named include, exactly
        // as it parsed before templates existed.
        var document = new TestNsqlParser("CREATE TABLE app.t (include bigint NOT NULL);").Parse();

        var column = document.Database.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem()
            .Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("include");
        column.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Expand_IncludeInsideSchemaTemplateTable_ComposesPerInstance()
    {
        // Act
        // Composition: a table declared by a schema template can include a table template; the include
        // re-targets to each instance at expansion.
        var app = ExpandIntoApp(
            """
            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at datetimeoffset NOT NULL
            END;
            TEMPLATE t
            BEGIN
              CREATE TABLE outbox (id uuid NOT NULL, INCLUDE audit_columns);
            END;
            """);

        // Assert
        app.Tables.ShouldHaveSingleItem().Columns.Select(c => c.Name.Value).ShouldBe(["id", "created_at"]);
    }
}
