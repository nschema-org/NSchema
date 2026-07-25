using System.Reflection;
using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Plan.Backends;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Tests.Plan;

/// <summary>
/// The base <see cref="SqlDialect"/> is an output surface: the snapshot pins, for every action, whether the
/// base renders standard SQL, demands the SQL from the dialect (abstract), or reports the action unsupported.
/// The fixture list carries one instance of every concrete <see cref="MigrationAction"/>, reflection-checked
/// for completeness, so a new action cannot ship without deciding its tier here.
/// </summary>
public sealed class SqlDialectTests
{
    private readonly TestDialect _sut = new();

    /// <summary>Implements only the abstract methods, with markers, so the base tiers show through.</summary>
    private class TestDialect : SqlDialect
    {
        protected override string Name => "TestDialect";

        private static Result<IReadOnlyList<SqlStatement>> Engine(MigrationAction action) =>
            Statement($"-- engine-specific {action.GetType().Name}");

        protected override Result<IReadOnlyList<SqlStatement>> CreateTable(CreateTable action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> AddColumn(AddColumn action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> AlterColumn(AlterColumn action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> AlterIdentitySequence(AlterIdentitySequence action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> SetColumnGenerated(SetColumnGenerated action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> CreateIndex(CreateIndex action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> DropIndex(DropIndex action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> CreateTrigger(CreateTrigger action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> DropTrigger(DropTrigger action) => Engine(action);
        protected override Result<IReadOnlyList<SqlStatement>> CreateView(CreateView action) => Engine(action);
    }

    private static SqlIdentifier N(string name) => new(name);

    /// <summary>One of every concrete action, plus extra instances covering branchy defaults.</summary>
    private static readonly IReadOnlyList<MigrationAction> Actions =
    [
        // Schemas
        new CreateSchema(N("app")),
        new DropSchema(N("app")),
        new RenameSchema(N("app"), N("core")),
        new GrantSchemaUsage(N("app"), N("readers")),
        new RevokeSchemaUsage(N("app"), N("readers")),
        new SetSchemaComment(N("app"), null, "Application schema"),

        // Tables
        new CreateTable(N("app"), new Table { Name = N("users"), Columns = { new Column { Name = N("id"), Type = SqlType.Int } } }),
        new DropTable(new ObjectAddress(N("app"), N("users"))),
        new RenameTable(new ObjectAddress(N("app"), N("users")), N("accounts")),
        new AddPrimaryKey(new ObjectAddress(N("app"), N("users")), new PrimaryKey { Name = N("pk_users"), ColumnNames = [N("id")] }),
        new DropPrimaryKey(new MemberAddress(N("app"), N("users"), N("pk_users"))),
        new AddForeignKey(new ObjectAddress(N("app"), N("orders")), new ForeignKey
        {
            Name = N("fk_orders_users"),
            ColumnNames = [N("user_id")],
            References = new ObjectAddress(N("app"), N("users")),
            ReferencedColumnNames = [N("id")],
            OnDelete = ReferentialAction.Cascade,
        }),
        new DropForeignKey(new MemberAddress(N("app"), N("orders"), N("fk_orders_users"))),
        new GrantTablePrivileges(new ObjectAddress(N("app"), N("users")), N("readers"), TablePrivilege.AppendOnly),
        new RevokeTablePrivileges(new ObjectAddress(N("app"), N("users")), N("readers"), TablePrivilege.All),
        new SetTableComment(new ObjectAddress(N("app"), N("users")), null, "User accounts"),

        // Columns
        new AddColumn(new ObjectAddress(N("app"), N("users")), new Column { Name = N("email"), Type = SqlType.VarChar(200) }),
        new DropColumn(new ObjectAddress(N("app"), N("users")), new Column { Name = N("email"), Type = SqlType.VarChar(200) }),
        new RenameColumn(new MemberAddress(N("app"), N("users"), N("email")), N("email_address")),
        new AlterColumn(new ObjectAddress(N("app"), N("users")), new Column { Name = N("age"), Type = SqlType.Int }, Type: new ValueChange<SqlType>(SqlType.SmallInt, SqlType.Int)),
        new AlterColumn(new ObjectAddress(N("app"), N("users")), new Column { Name = N("email"), Type = SqlType.Text }, Nullability: new ValueChange<bool>(true, false)),
        new AlterIdentitySequence(new MemberAddress(N("app"), N("users"), N("id")), null, new IdentityOptions(1, 1, 1)),
        new SetColumnDefault(new MemberAddress(N("app"), N("users"), N("age")), null, "0"),
        new SetColumnDefault(new MemberAddress(N("app"), N("users"), N("age")), "0", null),
        new SetColumnGenerated(new MemberAddress(N("app"), N("orders"), N("total")), null, "price * quantity"),
        new SetColumnComment(new MemberAddress(N("app"), N("users"), N("email")), null, "Primary contact"),

        // Constraints
        new AddCheckConstraint(new ObjectAddress(N("app"), N("users")), new CheckConstraint { Name = N("ck_age"), Expression = "age >= 0" }),
        new DropCheckConstraint(new MemberAddress(N("app"), N("users"), N("ck_age"))),
        new AddUniqueConstraint(new ObjectAddress(N("app"), N("users")), new UniqueConstraint { Name = N("uq_email"), ColumnNames = [N("email")] }),
        new DropUniqueConstraint(new MemberAddress(N("app"), N("users"), N("uq_email"))),
        new AddExclusionConstraint(new ObjectAddress(N("app"), N("bookings")), new ExclusionConstraint
        {
            Name = N("ex_overlap"),
            Elements = [new ExclusionElement("&&", N("period"))],
        }),
        new DropExclusionConstraint(new MemberAddress(N("app"), N("bookings"), N("ex_overlap"))),
        new SetConstraintComment(new MemberAddress(N("app"), N("users"), N("ck_age")), null, "Sanity check"),

        // Indexes
        new CreateIndex(new ObjectAddress(N("app"), N("users")), new TableIndex { Name = N("ix_users_email"), Columns = [new IndexColumn(N("email"))] }),
        new DropIndex(new MemberAddress(N("app"), N("users"), N("ix_users_email"))),
        new SetIndexComment(new MemberAddress(N("app"), N("users"), N("ix_users_email")), null, "Lookup index"),

        // Triggers
        new CreateTrigger(new ObjectAddress(N("app"), N("users")), new Trigger
        {
            Name = N("trg_audit"),
            Timing = TriggerTiming.After,
            Events = TriggerEvent.Insert,
            Body = "INSERT INTO app.audit VALUES (1)",
        }),
        new DropTrigger(new MemberAddress(N("app"), N("users"), N("trg_audit"))),
        new SetTriggerComment(new MemberAddress(N("app"), N("users"), N("trg_audit")), null, "Audit trail"),

        // Views
        new CreateView(N("app"), new View { Name = N("active_users"), Body = "SELECT * FROM app.users" }),
        new DropView(new ObjectAddress(N("app"), N("active_users"))),
        new DropView(new ObjectAddress(N("app"), N("user_stats")), IsMaterialized: true),
        new RenameView(new ObjectAddress(N("app"), N("active_users")), N("current_users")),
        new RenameView(new ObjectAddress(N("app"), N("user_stats")), N("account_stats"), IsMaterialized: true),
        new SetViewComment(new ObjectAddress(N("app"), N("active_users")), null, "Active only"),

        // Enums
        new CreateEnum(N("app"), new EnumType { Name = N("mood"), Values = { "happy", "sad" } }),
        new DropEnum(new ObjectAddress(N("app"), N("mood"))),
        new RenameEnum(new ObjectAddress(N("app"), N("mood")), N("feeling")),
        new AddEnumValue(new ObjectAddress(N("app"), N("mood")), "meh", After: "happy"),
        new SetEnumComment(new ObjectAddress(N("app"), N("mood")), null, "How it's going"),

        // Domains
        new CreateDomain(N("app"), new DomainType { Name = N("email_address"), DataType = SqlType.Text }),
        new DropDomain(new ObjectAddress(N("app"), N("email_address"))),
        new RenameDomain(new ObjectAddress(N("app"), N("email_address")), N("contact_address")),
        new RecreateDomain(N("app"), new DomainType { Name = N("email_address"), DataType = SqlType.VarChar(320) }),
        new AlterDomainDefault(new ObjectAddress(N("app"), N("email_address")), null, "''"),
        new AlterDomainNotNull(new ObjectAddress(N("app"), N("email_address")), true),
        new AddDomainCheck(new ObjectAddress(N("app"), N("email_address")), new CheckConstraint { Name = N("ck_at_sign"), Expression = "VALUE LIKE '%@%'" }),
        new DropDomainCheck(new MemberAddress(N("app"), N("email_address"), N("ck_at_sign"))),
        new SetDomainComment(new ObjectAddress(N("app"), N("email_address")), null, "An email address"),

        // Composite types
        new CreateCompositeType(N("app"), new CompositeType { Name = N("address"), Fields = { new CompositeField(N("street"), SqlType.Text) } }),
        new DropCompositeType(new ObjectAddress(N("app"), N("address"))),
        new RenameCompositeType(new ObjectAddress(N("app"), N("address")), N("postal_address")),
        new AddCompositeField(new ObjectAddress(N("app"), N("address")), new CompositeField(N("country"), SqlType.Text)),
        new DropCompositeField(new MemberAddress(N("app"), N("address"), N("country"))),
        new AlterCompositeFieldType(new MemberAddress(N("app"), N("address"), N("street")), SqlType.Text, SqlType.VarChar(200)),
        new SetCompositeTypeComment(new ObjectAddress(N("app"), N("address")), null, "A postal address"),

        // Sequences
        new CreateSequence(N("app"), new Sequence { Name = N("order_seq"), Options = new SequenceOptions(StartWith: 1, IncrementBy: 1) }),
        new DropSequence(new ObjectAddress(N("app"), N("order_seq"))),
        new RenameSequence(new ObjectAddress(N("app"), N("order_seq")), N("order_numbers")),
        new AlterSequence(new ObjectAddress(N("app"), N("order_seq")), new SequenceOptions(), new SequenceOptions(IncrementBy: 2)),
        new SetSequenceComment(new ObjectAddress(N("app"), N("order_seq")), null, "Order numbering"),

        // Routines
        new CreateRoutine(N("app"), new Routine
        {
            Name = N("add_tax"),
            RoutineKind = RoutineKind.Function,
            Arguments = "amount numeric",
            Definition = "RETURN amount * 1.2;",
        }),
        new DropRoutine(new ObjectAddress(N("app"), N("add_tax")), RoutineKind.Function),
        new RenameRoutine(new ObjectAddress(N("app"), N("add_tax")), N("apply_tax"), RoutineKind.Function),
        new RecreateRoutine(N("app"), new Routine
        {
            Name = N("add_tax"),
            RoutineKind = RoutineKind.Function,
            Arguments = "amount numeric, rate numeric",
            Definition = "RETURN amount * rate;",
        }),
        new SetRoutineComment(new ObjectAddress(N("app"), N("add_tax")), null, "VAT", RoutineKind.Function),

        // Extensions
        new CreateExtension(new Extension { Name = N("uuid-ossp") }),
        new DropExtension(N("uuid-ossp")),
        new AlterExtension(N("uuid-ossp"), "1.0", "1.1"),
        new SetExtensionComment(N("uuid-ossp"), null, "UUID generation"),

        // Scripts
        new ExecuteScript(new DeploymentScript(N("seed"), "INSERT INTO app.users VALUES (1)", null, DeploymentPhase.Post)),
    ];

    [Fact]
    public Task Generate_EveryAction_RendersItsTier()
    {
        // Act
        var rendered = Actions.Select(action =>
        {
            var result = _sut.Generate(action);
            return new
            {
                Action = action.GetType().Name,
                Statements = result.Value?
                    .Select(s => s.RunOutsideTransaction ? $"{s.Sql.Value} [outside transaction]" : s.Sql.Value)
                    .ToList(),
                Diagnostics = result.Diagnostics.Select(d => $"{d.Severity}: {d.Message}").ToList(),
            };
        });

        // Assert
        return Verify(rendered);
    }

    [Fact]
    public void Actions_CoverEveryConcreteMigrationAction()
    {
        // Arrange
        var known = typeof(MigrationAction).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MigrationAction).IsAssignableFrom(t))
            .ToList();

        // Assert — every concrete action has a fixture; a new action must be added above (and tiered).
        Actions.Select(a => a.GetType()).Distinct().ShouldBe(known, ignoreOrder: true);
    }

    [Fact]
    public void SqlDialect_DeclaresOneSameNamedMethodPerAction()
    {
        // Arrange
        var actionTypes = typeof(MigrationAction).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MigrationAction).IsAssignableFrom(t));

        foreach (var actionType in actionTypes)
        {
            // Act — the dispatch convention: a protected method named after the action, taking it.
            var method = typeof(SqlDialect).GetMethod(actionType.Name, BindingFlags.Instance | BindingFlags.NonPublic, [actionType]);

            // Assert
            method.ShouldNotBeNull($"SqlDialect has no method '{actionType.Name}({actionType.Name})'");
            method.ReturnType.ShouldBe(typeof(Result<IReadOnlyList<SqlStatement>>));
        }
    }

    [Fact]
    public void Generate_QuotesEmbeddedQuotes()
    {
        // Act
        var result = _sut.Generate(new DropTable(new ObjectAddress("app", "we\"ird")));

        // Assert
        result.Require().ShouldHaveSingleItem().Sql.Value.ShouldBe("DROP TABLE \"app\".\"we\"\"ird\"");
    }

    [Fact]
    public void Generate_UnknownActionType_FailsWithTheCatalogedDiagnostic()
    {
        // Arrange
        var action = new BogusAction();

        // Act
        var result = _sut.Generate(action);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(SqlDialectDiagnostics.Unknown(action, "TestDialect"));
    }

    private sealed record BogusAction : MigrationAction;

    [Fact]
    public void Skipped_IsAWarningAndAnEmptyRendering()
    {
        // Arrange — a dialect deciding a comment is ignorable rather than an error.
        var action = new SetTableComment(new ObjectAddress(N("app"), N("users")), null, "User accounts");
        var dialect = new SkippingDialect();

        // Act
        var result = dialect.Generate(action);

        // Assert — the plan proceeds without the change, carrying the warning.
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(SqlDialectDiagnostics.Skipped(action, "TestDialect"));
    }

    private sealed class SkippingDialect : TestDialect
    {
        protected override Result<IReadOnlyList<SqlStatement>> SetTableComment(SetTableComment action) => Skipped(action);
    }

    [Fact]
    public void AnsiSequenceBuilders_RenderTheStandardForms()
    {
        // Arrange — a dialect opting in to sequences via the ANSI builders.
        var dialect = new AnsiSequenceDialect();
        var sequence = new Sequence
        {
            Name = "order_seq",
            Options = new SequenceOptions(StartWith: 10, IncrementBy: 5, MaxValue: 1000, Cycle: true),
        };

        // Act
        var create = dialect.Generate(new CreateSequence("app", sequence));
        var alter = dialect.Generate(new AlterSequence(new ObjectAddress("app", "order_seq"), new SequenceOptions(), new SequenceOptions(IncrementBy: 2)));
        var drop = dialect.Generate(new DropSequence(new ObjectAddress("app", "order_seq")));

        // Assert
        create.Require().ShouldHaveSingleItem().Sql.Value.ShouldBe("CREATE SEQUENCE \"app\".\"order_seq\" START WITH 10 INCREMENT BY 5 MAXVALUE 1000 CYCLE");
        alter.Require().ShouldHaveSingleItem().Sql.Value.ShouldBe("ALTER SEQUENCE \"app\".\"order_seq\" INCREMENT BY 2");
        drop.Require().ShouldHaveSingleItem().Sql.Value.ShouldBe("DROP SEQUENCE \"app\".\"order_seq\"");
    }

    private sealed class AnsiSequenceDialect : TestDialect
    {
        protected override Result<IReadOnlyList<SqlStatement>> CreateSequence(CreateSequence action) => AnsiCreateSequence(action);
        protected override Result<IReadOnlyList<SqlStatement>> AlterSequence(AlterSequence action) => AnsiAlterSequence(action);
        protected override Result<IReadOnlyList<SqlStatement>> DropSequence(DropSequence action) => AnsiDropSequence(action);
    }
}
