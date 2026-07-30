using System.Text;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using NSchema.State;
using NSchema.State.Domain;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class NsqlWriterTests
{
    private static string WriteOneTable(Table table)
        => NsqlWriter.Write(new Database { Schemas = [new Schema { Name = "app", Tables = [table] }] });

    // Canonicalize a schema to a deterministic string for structural-equality comparison,
    // using the internal state serializer (independent of the DDL writer under test).
    private static string Canonical(Database schema)
        => Encoding.UTF8.GetString(new DatabaseStateSerializer().Serialize(new DatabaseState(schema)).Span);

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------


    /// <summary>Writes an empty-schema project carrying only <paramref name="directives"/>.</summary>
    private static string WriteDirectives(ProjectDirectives directives, params Schema[] schemas) =>
        NsqlWriter.Write(new Database { Schemas = [.. schemas] }, directives);

    private static ObjectAddress InApp(string name) => new("app", name);

    [Fact]
    public void Write_NotNullColumn_EmitsNotNull()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }] }).ShouldContain("id int NOT NULL");

    [Fact]
    public void Write_SchemaQualifiedColumnType_EmitsTheQualifier()
        // The qualifier is a component now; writing it reads schema and name straight across, no string split.
        => WriteOneTable(new Table
        {
            Name = "t",
            Columns = [new Column { Name = "state", Type = SqlType.Custom("app", "status") }],
        })
            .ShouldContain("state app.status NOT NULL");

    [Fact]
    public void Write_ParameterisedColumnType_EmitsFacetsFromComponents()
        // Facets are structural too, so a parameterised type round-trips without the writer parsing text.
        => WriteOneTable(new Table
        {
            Name = "t",
            Columns = [new Column { Name = "code", Type = SqlType.VarChar(64) }],
        })
            .ShouldContain("code varchar(64) NOT NULL");

    [Fact]
    public void Write_NullableColumn_OmitsNullKeyword()
    {
        var ddl = WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "note", Type = SqlType.Text, IsNullable = true }] });
        ddl.ShouldContain("note text");
        ddl.ShouldNotContain("note text NULL");
        ddl.ShouldNotContain("note text NOT NULL");
    }

    [Fact]
    public void Write_IdentityWithOptions_EmitsInStartIncrementMinValueOrder()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 5, 2) }] })
            .ShouldContain("id bigint NOT NULL IDENTITY (START 1, INCREMENT 2, MINVALUE 5)");

    [Fact]
    public void Write_BareIdentity_EmitsNoParens()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true }] })
            .ShouldContain("id bigint NOT NULL IDENTITY\n");

    [Fact]
    public void Write_Default_IsEmitted()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "flag", Type = SqlType.Int, DefaultExpression = "0" }] })
            .ShouldContain("flag int NOT NULL DEFAULT 0");

    [Fact]
    public void Write_ColumnRenameDirective_IsEmitted()
        => WriteDirectives(new ProjectDirectives(MemberRenames: [new MemberRenameDirective(new MemberAddress("app", "t", "legacy_flag"), "flag")]))
            .ShouldContain("RENAME COLUMN app.t.legacy_flag TO flag;");

    [Fact]
    public void Write_ParameterisedType_RendersFacets()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "amount", Type = SqlType.Decimal(18, 2) }] })
            .ShouldContain("amount decimal(18,2)");

    // -------------------------------------------------------------------------
    // Constraints and indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_PrimaryKey_IsEmitted()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }], PrimaryKey = new PrimaryKey { Name = "t_pk", ColumnNames = ["id"] } })
            .ShouldContain("CONSTRAINT t_pk PRIMARY KEY (id)");

    [Fact]
    public void Write_ForeignKeyWithActions_IsEmitted()
        => WriteOneTable(new Table
        {
            Name = "orders",
            Columns = [new Column { Name = "user_id", Type = SqlType.Int }],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"], OnDelete = ReferentialAction.Cascade, OnUpdate = ReferentialAction.SetNull }],
        })
            .ShouldContain("CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE ON UPDATE SET NULL");

    [Fact]
    public void Write_ForeignKeyWithoutActions_OmitsOnClauses()
        => WriteOneTable(new Table
        {
            Name = "orders",
            Columns = [new Column { Name = "user_id", Type = SqlType.Int }],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }],
        })
            .ShouldNotContain("ON DELETE");

    [Fact]
    public void Write_Check_WrapsExpressionInParens()
        => WriteOneTable(new Table
        {
            Name = "t",
            Columns = [new Column { Name = "age", Type = SqlType.Int }],
            CheckConstraints = [new CheckConstraint { Name = "chk", Expression = "age >= 0" }],
        })
            .ShouldContain("CONSTRAINT chk CHECK (age >= 0)");

    [Fact]
    public void Write_PartialUniqueIndex_IsEmitted()
        => WriteOneTable(new Table
        {
            Name = "t",
            Columns = [new Column { Name = "email", Type = SqlType.Text }],
            Indexes = [new TableIndex { Name = "ux", Columns = ["email"], IsUnique = true, Predicate = "deleted_at IS NULL" }],
        })
            .ShouldContain("UNIQUE INDEX ux(email) WHERE (deleted_at IS NULL)");

    // -------------------------------------------------------------------------
    // Comments, schemas, grants, drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ColumnComment_EmitsDocComment()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "email", Type = SqlType.Text, Comment = "Primary contact." }] })
            .ShouldContain("--- Primary contact.\n");

    [Fact]
    public void Write_MultiLineComment_EmitsOneDocLinePerLine()
        => WriteOneTable(new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }], Comment = "Line one.\nLine two." })
            .ShouldContain("--- Line one.\n--- Line two.\n");

    [Fact]
    public void Write_SchemaRenameDirective_IsEmitted()
    {
        var ddl = WriteDirectives(new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(DatabaseAddress.Schema("legacy"), DatabaseAddress.Schema("app"))]),
            new Schema { Name = "app" });
        ddl.ShouldContain("CREATE SCHEMA app;");
        ddl.ShouldContain("RENAME SCHEMA legacy TO app;");
    }

    [Fact]
    public void Write_TableGrant_IsEmitted()
        => WriteOneTable(new Table
        {
            Name = "t",
            Columns = [new Column { Name = "id", Type = SqlType.Int }],
            Grants = [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)],
        })
            .ShouldContain("GRANT SELECT, INSERT ON app.t TO readers;");

    [Fact]
    public void Write_SchemaGrant_IsEmitted()
        => NsqlWriter.Write(new Database { Schemas = [new Schema { Name = "app", Grants = [new SchemaGrant("app_role")] }] })
            .ShouldContain("GRANT USAGE ON SCHEMA app TO app_role;");

    [Fact]
    public void Write_WithoutSchemaDeclarations_EmitsOnlyMemberObjects()
    {
        // Arrange
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }],
        };

        // Act
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(schema, declareSchemas: false));

        // Assert
        ddl.ShouldNotContain("CREATE SCHEMA");
        ddl.ShouldStartWith("CREATE TABLE app.t");
    }

    [Fact]
    public void Write_WithoutSchemaDeclarations_RoundTripsThroughParse()
    {
        // The reader vivifies the schema from the objects' qualified names, so a declaration-free file
        // reads back to the same members.
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }] }],
            Views = [new View { Name = "active", Body = "SELECT 1" }] }],
        };

        var reparsed = new TestNsqlParser(NsqlWriter.Write(SyntaxBuilder.Build(schema, declareSchemas: false))).Parse().Database;

        var app = reparsed.Schemas.ShouldHaveSingleItem();
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("t");
        app.Views.ShouldHaveSingleItem().Name.ShouldBe("active");
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_ThenParse_PreservesModelStructurally()
    {
        var original = TestData.RichSchema();
        var reparsed = new TestNsqlParser(NsqlWriter.Write(original)).Parse().Database;
        Canonical(reparsed).ShouldBe(Canonical(original));
    }

    [Fact]
    public void Write_IsStableThroughParseRoundTrip()
    {
        var ddl = NsqlWriter.Write(TestData.RichSchema());
        var reEmitted = NsqlWriter.Write(new TestNsqlParser(ddl).Parse().Database);
        reEmitted.ShouldBe(ddl);
    }

    [Fact]
    public Task Write_RichSchema_MatchesSnapshot() => Verify(NsqlWriter.Write(TestData.RichSchema(), TestData.RichDirectives()));

    // -------------------------------------------------------------------------
    // Triggers
    // -------------------------------------------------------------------------

    private static string WriteTriggerOn(Trigger trigger)
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }], Triggers = [trigger] }] }],
        });

    [Fact]
    public void Write_Trigger_EmitsStandaloneCreateTriggerAfterTable()
    {
        var ddl = WriteTriggerOn(new Trigger
        {
            Name = "audit",
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Insert | TriggerEvent.Update,
            Function = new RoutineReference("app", "log"),
            Level = TriggerLevel.Row,
        });
        ddl.ShouldContain("CREATE TRIGGER audit AFTER INSERT OR UPDATE ON app.users FOR EACH ROW EXECUTE FUNCTION app.log();");
    }

    [Fact]
    public void Write_TriggerWithUpdateOfWhenAndComment_IsEmitted()
    {
        var ddl = WriteTriggerOn(new Trigger
        {
            Name = "audit",
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Update,
            Function = new RoutineReference("app", "log"),
            Level = TriggerLevel.Row,
            UpdateOfColumns = ["email"],
            When = "new.email IS NOT NULL",
            Comment = "audit",
        });
        ddl.ShouldContain("--- audit\nCREATE TRIGGER audit AFTER UPDATE OF (email) ON app.users FOR EACH ROW WHEN (new.email IS NOT NULL) EXECUTE FUNCTION app.log();");
    }

    [Fact]
    public void Write_InsteadOfTrigger_IsEmitted()
        => WriteTriggerOn(new Trigger { Name = "v_ins", Timing = TriggerTiming.InsteadOf, Events = TriggerEvent.Insert, Function = new RoutineReference("app", "f"), Level = TriggerLevel.Row })
            .ShouldContain("CREATE TRIGGER v_ins INSTEAD OF INSERT ON app.users FOR EACH ROW EXECUTE FUNCTION app.f();");

    [Fact]
    public void Write_Trigger_RoundTripsThroughParse()
    {
        var trigger = new Trigger
        {
            Name = "audit",
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Insert | TriggerEvent.Delete,
            Function = new RoutineReference("app", "log"),
            Level = TriggerLevel.Row,
            When = "true",
            FunctionArguments = "'x'",
            Comment = "note",
        };
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }], Triggers = [trigger] }] }],
        };

        var reparsed = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database;
        var roundTripped = reparsed.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem();
        roundTripped.ShouldBe(trigger);            // structural equality (excludes the comment)
        roundTripped.Comment.ShouldBe("note");     // ... so assert the comment round-tripped too
    }

    [Fact]
    public void Write_InlineBodyTrigger_EmitsDollarQuotedBody()
        => WriteTriggerOn(new Trigger
        {
            Name = "audit",
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Insert,
            Body = "BEGIN INSERT INTO app.log VALUES (1); END",
        })
            .ShouldContain("CREATE TRIGGER audit AFTER INSERT ON app.users AS $$\nBEGIN INSERT INTO app.log VALUES (1); END\n$$;");

    [Fact]
    public void Write_InlineBodyTrigger_RoundTripsThroughParse()
    {
        // A body with its own semicolons survives because it is emitted (and lexed) as one dollar-quoted block.
        var trigger = new Trigger
        {
            Name = "audit",
            Timing = TriggerTiming.InsteadOf,
            Events = TriggerEvent.Insert | TriggerEvent.Update,
            Body = "BEGIN\n  UPDATE app.users SET id = id;\n  INSERT INTO app.log VALUES (1);\nEND",
            Comment = "note",
        };
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }], Triggers = [trigger] }] }],
        };

        var reparsed = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database;
        var roundTripped = reparsed.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Triggers.ShouldHaveSingleItem();
        roundTripped.ShouldBe(trigger);
        roundTripped.Body.ShouldBe(trigger.Body);
    }

    // -------------------------------------------------------------------------
    // Extensions (database-global, root-level)
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Extension_EmitsCreateExtension()
        => NsqlWriter.Write(new Database { Extensions = [new Extension { Name = "citext" }] })
            .ShouldContain("CREATE EXTENSION citext;");

    [Fact]
    public void Write_ExtensionWithVersion_EmitsVersionClause()
        => NsqlWriter.Write(new Database { Extensions = [new Extension { Name = "postgis", Version = "3.4" }] })
            .ShouldContain("CREATE EXTENSION postgis VERSION '3.4';");

    [Fact]
    public void Write_ExtensionWithNonIdentifierName_QuotesIt()
        // A hyphenated name (e.g. uuid-ossp) must be quoted so it round-trips through the parser.
        => NsqlWriter.Write(new Database { Extensions = [new Extension { Name = "uuid-ossp" }] })
            .ShouldContain("CREATE EXTENSION \"uuid-ossp\";");

    [Fact]
    public void Write_ExtensionComment_EmitsDocComment()
        => NsqlWriter.Write(new Database { Extensions = [new Extension { Name = "postgis", Comment = "spatial types" }] })
            .ShouldContain("--- spatial types\nCREATE EXTENSION postgis;");

    [Fact]
    public void Write_Extension_RoundTripsThroughParse()
    {
        var schema = new Database { Extensions = [new Extension { Name = "citext" }, new Extension { Name = "uuid-ossp", Comment = "ids" }, new Extension { Name = "postgis", Version = "3.4" }] };
        var reparsed = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database;
        reparsed.Extensions.ShouldBe(schema.Extensions);
    }

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_View_EmitsCreateViewWithBody()
    {
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "app", Views = [new View { Name = "active", Body = "SELECT id FROM app.users WHERE active" }] },
        ],
        };
        NsqlWriter.Write(schema).ShouldContain("CREATE VIEW app.active AS SELECT id FROM app.users WHERE active;");
    }

    [Fact]
    public void Write_View_RoundTripsThroughParse()
    {
        var source = "CREATE SCHEMA app;\n\nCREATE VIEW app.active AS SELECT id, name FROM app.users WHERE active;\n";
        var reEmitted = NsqlWriter.Write(new TestNsqlParser(source).Parse().Database);
        var reparsed = new TestNsqlParser(reEmitted).Parse().Database;

        var view = reparsed.Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();
        view.Name.ShouldBe("active");
        view.Body.ShouldBe("SELECT id, name FROM app.users WHERE active");
        view.DependsOn.ShouldHaveSingleItem().ShouldBe(new ObjectAddress("app", "users"));
    }

    // -------------------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_SimpleDomain_EmitsCreateDomain()
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            Domains = [new DomainType { Name = "typeid", DataType = SqlType.Text }] }],
        })
            .ShouldContain("CREATE DOMAIN app.typeid AS text;");

    [Fact]
    public void Write_DomainWithEveryClause_EmitsInCanonicalOrder()
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            Domains = [new DomainType { Name = "email", DataType = SqlType.Text, Default = "'x@y'", NotNull = true,
                Checks = [new CheckConstraint { Name = "email_fmt", Expression = "VALUE ~ '@'" }] }] }],
        })
            // NOT NULL, then checks, then DEFAULT (last, so its opaque expr reads back to the ';').
            .ShouldContain("CREATE DOMAIN app.email AS text NOT NULL CONSTRAINT email_fmt CHECK (VALUE ~ '@') DEFAULT 'x@y';");

    [Fact]
    public void Write_Domain_RoundTripsThroughParse()
    {
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Domains = [new DomainType { Name = "email", DataType = SqlType.Text, Default = "'x@y'", NotNull = true,
                Checks = [new CheckConstraint { Name = "email_fmt", Expression = "VALUE ~ '@'" }], Comment = "an email" }] }],
        };

        var domain = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().Domains.ShouldHaveSingleItem();
        domain.DataType.ShouldBe(SqlType.Text);
        domain.NotNull.ShouldBeTrue();
        domain.Default.ShouldBe("'x@y'");
        domain.Checks.ShouldHaveSingleItem().Name.ShouldBe("email_fmt");
    }

    // -------------------------------------------------------------------------
    // Composite types
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_SimpleCompositeType_EmitsCreateType()
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            CompositeTypes = [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)] }] }],
        })
            .ShouldContain("CREATE TYPE app.address AS (street text, zip int);");

    [Fact]
    public void Write_CompositeType_RoundTripsThroughParse()
    {
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            CompositeTypes = [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)], Comment = "a postal address" }] }],
        };

        var type = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().CompositeTypes.ShouldHaveSingleItem();
        type.Name.ShouldBe("address");
        type.Fields.Count.ShouldBe(2);
        type.Fields[0].DataType.ShouldBe(SqlType.Text);
        type.Fields[1].DataType.ShouldBe(SqlType.Int);
    }

    // -------------------------------------------------------------------------
    // Materialized views
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_MaterializedView_EmitsMaterializedKeyword()
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            Views = [new View { Name = "daily", Body = "SELECT 1", IsMaterialized = true }] }],
        })
            .ShouldContain("CREATE MATERIALIZED VIEW app.daily AS SELECT 1;");

    [Fact]
    public void Write_MaterializedViewIndex_EmitsStandaloneCreateIndex()
        => NsqlWriter.Write(new Database
        {
            Schemas = [new Schema { Name = "app",
            Views = [new View { Name = "daily", Body = "SELECT x FROM app.t", IsMaterialized = true,
                Indexes = [new TableIndex { Name = "daily_ix", Columns = ["x"], IsUnique = true, Predicate = "x IS NOT NULL" }] }] }],
        })
            .ShouldContain("CREATE UNIQUE INDEX daily_ix ON app.daily(x) WHERE (x IS NOT NULL);");

    [Fact]
    public void Write_MaterializedView_RoundTripsThroughParse()
    {
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app",
            Views = [new View { Name = "daily", Body = "SELECT x FROM app.t", IsMaterialized = true,
                Indexes = [new TableIndex { Name = "daily_ix", Columns = ["x"] }] }] }],
        };

        var view = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();
        view.IsMaterialized.ShouldBeTrue();
        view.Indexes.ShouldHaveSingleItem().Name.ShouldBe("daily_ix");
    }

    // -------------------------------------------------------------------------
    // Enums and sequences
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Enum_EmitsQuotedValueList()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["pending", "shipped"] }] },
        ],
        }).ShouldContain("CREATE ENUM app.status('pending', 'shipped');");

    [Fact]
    public void Write_EnumValueWithQuote_EscapesIt()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["it's"] }] },
        ],
        }).ShouldContain("CREATE ENUM app.status('it''s');");

    [Fact]
    public void Write_Sequence_WithoutOptions_OmitsParens()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Sequences = [new Sequence { Name = "order_id" }] },
        ],
        }).ShouldContain("CREATE SEQUENCE app.order_id;");

    [Fact]
    public void Write_Sequence_EmitsOptionsInCanonicalOrder()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Sequences = [
                new Sequence { Name = "order_id", Options = new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5,
                    MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true) },
            ] },
        ],
        }).ShouldContain("CREATE SEQUENCE app.order_id(AS bigint, START 100, INCREMENT 5, MINVALUE -10, MAXVALUE 999999, CACHE 10, CYCLE);");

    // -------------------------------------------------------------------------
    // Functions and procedures
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_Function_EmitsArgumentsAndDefinitionVerbatim()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Routines = [new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric", Definition = "RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$" }] },
        ],
        }).ShouldContain("CREATE FUNCTION app.add_tax(amount numeric) RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$;");

    [Fact]
    public void Write_Function_MultiLineDefinition_KeepsNewlines()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Routines = [new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$" }] },
        ],
        }).ShouldContain("CREATE FUNCTION app.f() RETURNS int LANGUAGE sql AS $$\n  SELECT 1;\n$$;");

    [Fact]
    public void Write_Function_TrailingWhitespaceInDefinition_IsTrimmed()
        // A code-built definition ending in whitespace must not push the ';' onto dangling whitespace.
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Routines = [new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int AS $$ SELECT 1 $$  \n" }] },
        ],
        }).ShouldContain("AS $$ SELECT 1 $$;");

    [Fact]
    public void Write_Procedure_IsEmitted()
        => NsqlWriter.Write(new Database
        {
            Schemas = [
            new Schema { Name = "app", Routines = [new Routine { Name = "archive", RoutineKind = RoutineKind.Procedure, Arguments = "before date", Definition = "LANGUAGE sql AS $$ DELETE $$" }] },
        ],
        }).ShouldContain("CREATE PROCEDURE app.archive(before date) LANGUAGE sql AS $$ DELETE $$;");

}
