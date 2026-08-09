using Microsoft.Extensions.Options;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Policies;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Policies;

namespace NSchema.Tests.Plan;

/// <summary>
/// The planner conducts the pipeline: it validates each stage's output and mechanically realizes the complete
/// diff as SQL. The diff-stage intelligence itself is covered by <see cref="Tests.Diff.ProjectComparerTests"/>.
/// </summary>
public sealed class MigrationPlannerTests
{
    private static readonly Database _emptySchema = new Database { Schemas = [] };
    private static readonly DatabaseDiff _emptyDiff = new([]);
    private static readonly CurrentState _current = new(_emptySchema);
    private static readonly ProjectDefinition _desired = new(_emptySchema);

    private readonly IProjectComparer _differ = Substitute.For<IProjectComparer>();
    private readonly IPlanLinearizer _linearizer = Substitute.For<IPlanLinearizer>();
    private readonly List<IProjectPolicy> _projectPolicies = [];
    private readonly List<IPlanPolicy> _planPolicies = [];

    private readonly DiagnosticOptions _diagnosticOptions = new();

    private MigrationPlanner Sut => new(Options.Create(_diagnosticOptions), _differ, _linearizer, _projectPolicies, _planPolicies, new SqlEquivalence(), new StubSqlDialect());

    public MigrationPlannerTests()
    {
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(_emptyDiff, []));
        _linearizer.Linearize(Arg.Any<DatabaseDiff>(), Arg.Any<PlanDependencies>(), Arg.Any<DialectCapabilities>()).Returns(_ => []);
    }

    /// <summary>A difference touching two schemas, so a scope has something to narrow away.</summary>
    private static DatabaseDiff TwoSchemaDiff() => new(
    [
        SchemaDiff.Removed("app"),
        SchemaDiff.Removed("billing"),
    ]);

    [Fact]
    public void Plan_ComparesTheWholeStates_ThenNarrowsTheDifferenceToTheScope()
    {
        // Arrange — the comparer answers what differs between two whole states, a complete question that
        // needs no scope; the planner applies the scope to its answer.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.To(DatabaseAddress.Schema("app")));

        // Assert
        result.Value!.Diff.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Plan_RunsPlanPoliciesAgainstTheNarrowedDiff_NotTheWholeComparison()
    {
        // Arrange — comparing whole states manufactures a removal for every out-of-scope schema. If policies
        // saw those, an ordinary scoped plan would report changes it is never going to make.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));
        var policy = Substitute.For<IPlanPolicy>();
        _planPolicies.Add(policy);

        // Act
        Sut.Plan(_current, _desired, PlanningScope.To(DatabaseAddress.Schema("app")));

        // Assert
        policy.Received(1).Validate(Arg.Is<MigrationPlan>(p => p!.Diff.Schemas.Count == 1
            && p.Diff.Schemas[0].Name == "app"));
    }

    [Fact]
    public void Plan_LinearizesOnlyTheNarrowedDiff()
    {
        // Arrange — the SQL must not contain out-of-scope work either.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(TwoSchemaDiff(), []));

        // Act
        Sut.Plan(_current, _desired, PlanningScope.To(DatabaseAddress.Schema("app")));

        // Assert
        _linearizer.Received(1).Linearize(Arg.Is<DatabaseDiff>(d => d!.Schemas.Count == 1), Arg.Any<PlanDependencies>(), Arg.Any<DialectCapabilities>());
    }

    [Fact]
    public void Validate_RunsProjectPoliciesAgainstTheProject()
    {
        // Arrange
        var desired = new ProjectDefinition(new Database { Schemas = [new Schema { Name = "app" }] });
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(desired).Returns([Diagnostic.Error("test", "bad-schema", "bad schema")]);
        _projectPolicies.Add(policy);

        // Act
        var diagnostics = Sut.Validate(desired);

        // Assert
        diagnostics.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
        policy.Received(1).Validate(desired);
    }

    [Fact]
    public void Plan_SchemaPolicyError_BlocksButStillCarriesTheCompletePlan()
    {
        // Arrange
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(Arg.Any<ProjectDefinition>()).Returns([Diagnostic.Error("test", "bad-schema", "bad schema")]);
        _projectPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert: a policy block means "may not apply", not "stopped computing" — the failure carries the
        // complete plan so the offending change stays visible.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        _differ.Received(1).Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public void Plan_NonFatalSchemaDiagnostics_FlowIntoResult()
    {
        // Arrange
        var policy = Substitute.For<IProjectPolicy>();
        policy.Validate(Arg.Any<ProjectDefinition>())
            .Returns([new Diagnostic("test", "policy-finding", "lint", DiagnosticSeverity.Warning)]);
        _projectPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert: a non-error schema finding is carried through alongside the plan.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public void Plan_PassesCurrentAndDesiredToTheDiffer()
    {
        // Act
        Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — the current side the differ sees carries the same ledger and managed set.
        _differ.Received(1).Compare(
            Arg.Is<CurrentState>(c => c!.ExecutedScripts == _current.ExecutedScripts && c.Managed == _current.Managed),
            _desired);
    }

    [Fact]
    public void Plan_FiltersUnmanagedCurrentObjectsOutOfTheCompare()
    {
        // Arrange — the observation holds a managed and an unmanaged table; only the managed one (and anything
        // declared) is the plan's business.
        SqlIdentifier app = "app";
        var current = new CurrentState(new Database { Schemas = [new Schema { Name = app, Tables = [new Table { Name = "mine" }, new Table { Name = "theirs" }] }] })
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema(app)],
                SchemaObjects: [ObjectAddress.Table(app, "mine")]),
        };

        // Act
        Sut.Plan(current, _desired, PlanningScope.All);

        // Assert
        _differ.Received(1).Compare(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Single().Tables.Single().Name == "mine"),
            Arg.Any<ProjectDefinition>());
    }

    /// <summary>
    /// A schema NSchema neither manages nor declares is not the plan's business, and the managed set alone is
    /// what says so — no scope is involved. This is what an unscoped run leans on, and what a schema list
    /// derived from the project and the state used to be credited with keeping safe.
    /// </summary>
    [Fact]
    public void Plan_UnscopedRun_LeavesAnUnmanagedSchemaOutOfTheCompare()
    {
        // Arrange — `app` is managed; `unmanaged` is in the observation and is neither managed nor declared.
        SqlIdentifier app = "app";
        var current = new CurrentState(new Database
        {
            Schemas = [new Schema { Name = app }, new Schema { Name = "unmanaged" }],
        })
        {
            Managed = new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema(app)]),
        };

        // Act
        Sut.Plan(current, _desired, PlanningScope.All);

        // Assert — it never reaches the comparer, so nothing downstream can propose dropping it.
        _differ.Received(1).Compare(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.All(s => s.Name != "unmanaged")),
            Arg.Any<ProjectDefinition>());
    }

    [Fact]
    public void Plan_ManagedAfterApply_IsTheDeclaredIdentities()
    {
        // Arrange
        SqlIdentifier app = "app";
        var desired = new ProjectDefinition(new Database { Schemas = [new Schema { Name = app, Tables = [new Table { Name = "users" }] }] });

        // Act
        var plan = Sut.Plan(_current, desired, PlanningScope.All).Value!;

        // Assert — within scope, management after an apply is exactly what the project declares.
        plan.Managed.Schemas.Select(s => s.Name).ShouldBe([app]);
        plan.Managed.SchemaObjects.ShouldBe([ObjectAddress.Table(app, "users")]);
    }

    [Fact]
    public void Plan_ManagedAfterApply_LeavesAnImplicitSchemaUnmanaged_ButManagesWhatIsInIt()
    {
        // Arrange — the project writes a table into `app` without ever declaring the schema itself.
        SqlIdentifier app = "app";
        var desired = new ProjectDefinition(new Database
        {
            Schemas = [new Schema { Name = app, IsImplicit = true, Tables = [new Table { Name = "users" }] }],
        });

        // Act
        var plan = Sut.Plan(_current, desired, PlanningScope.All).Value!;

        // Assert — a container NSchema was never asked to own is not something a teardown may drop.
        plan.Managed.Schemas.ShouldBeEmpty();
        plan.Managed.SchemaObjects.ShouldBe([ObjectAddress.Table(app, "users")]);
    }

    [Fact]
    public void Plan_ManagedAfterApply_RetainsOutOfScopeManagedIdentities()
    {
        // Arrange — billing is managed but out of scope, so this plan leaves its management untouched.
        SqlIdentifier app = "app";
        SqlIdentifier billing = "billing";
        var current = new CurrentState(_emptySchema)
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema(billing)],
                SchemaObjects: [ObjectAddress.Table(billing, "invoices")]),
        };
        var desired = new ProjectDefinition(new Database { Schemas = [new Schema { Name = app }] });

        // Act
        var plan = Sut.Plan(current, desired, PlanningScope.To(DatabaseAddress.Schema(app))).Value!;

        // Assert
        plan.Managed.Schemas.Select(s => s.Name).ShouldBe([app, billing], ignoreOrder: true);
        plan.Managed.SchemaObjects.ShouldHaveSingleItem().Schema.ShouldBe(billing);
    }

    [Fact]
    public void Plan_ObjectTargetedTeardown_ReleasesTheTarget_AndKeepsItsSchemaManaged()
    {
        // Arrange — targeting one object converges only it towards nothing: the container and its siblings
        // stay managed, because an object entry covers nothing above or beside itself.
        SqlIdentifier app = "app";
        var users = ObjectAddress.Table(app, "users");
        var orders = ObjectAddress.Table(app, "orders");
        var current = new CurrentState(_emptySchema)
        {
            Managed = new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema(app)], SchemaObjects: [users, orders]),
        };

        // Act
        var plan = Sut.Plan(current, new ProjectDefinition(new Database()), PlanningScope.To([users])).Value!;

        // Assert
        plan.Managed.Schemas.Select(s => s.Name).ShouldBe([app]);
        plan.Managed.SchemaObjects.ShouldBe([orders]);
    }

    [Fact]
    public void Plan_Teardown_EmptiesTheManagedSet()
    {
        // Arrange — a teardown converges towards nothing: everything in scope stops being managed.
        SqlIdentifier app = "app";
        var current = new CurrentState(_emptySchema)
        {
            Managed = new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema(app)]),
        };

        // Act — an unrestricted teardown's scope covers every managed schema (derived by the workflow).
        var plan = Sut.Plan(current, new ProjectDefinition(new Database()), PlanningScope.To(DatabaseAddress.Schema(app))).Value!;

        // Assert
        plan.Managed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Plan_DeclaredAfterApply_RecordsTheProjectSpellings()
    {
        // Arrange
        SqlIdentifier app = "app";
        var desired = new ProjectDefinition(new Database
        {
            Schemas = [new Schema { Name = app, Views = [new View { Name = "active", Body = "SELECT id FROM users" }] }],
        });

        // Act
        var plan = Sut.Plan(_current, desired, PlanningScope.All).Value!;

        // Assert — an apply of this plan records exactly what the project spells.
        plan.Declared.Views.ShouldHaveSingleItem().ShouldBe(new ViewDefinition(ObjectAddress.View(app, "active"), "SELECT id FROM users"));
    }

    [Fact]
    public void Plan_DeclaredAfterApply_ExcludesImplicitObjects()
    {
        // Arrange — an implicit object is here for reference, not for this project to own or respell.
        SqlIdentifier app = "app";
        var desired = new ProjectDefinition(new Database
        {
            Schemas = [new Schema { Name = app, Views = [new View { Name = "active", Body = "SELECT 1", IsImplicit = true }] }],
        });

        // Act
        var plan = Sut.Plan(_current, desired, PlanningScope.All).Value!;

        // Assert
        plan.Declared.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Plan_DeclaredAfterApply_RetainsOutOfScopeSpellings()
    {
        // Arrange — billing's spelling is recorded but out of scope, so this plan carries it forward untouched.
        SqlIdentifier app = "app";
        SqlIdentifier billing = "billing";
        var current = new CurrentState(_emptySchema)
        {
            Managed = new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema(billing)], SchemaObjects: [ObjectAddress.View(billing, "invoices")]),
            Declared = new DefinitionSet([new ViewDefinition(ObjectAddress.View(billing, "invoices"), "SELECT 1")]),
        };
        var desired = new ProjectDefinition(new Database
        {
            Schemas = [new Schema { Name = app, Views = [new View { Name = "active", Body = "SELECT 2" }] }],
        });

        // Act
        var plan = Sut.Plan(current, desired, PlanningScope.To(DatabaseAddress.Schema(app))).Value!;

        // Assert
        plan.Declared.Views.ShouldBe([
            new ViewDefinition(ObjectAddress.View(app, "active"), "SELECT 2"),
            new ViewDefinition(ObjectAddress.View(billing, "invoices"), "SELECT 1"),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Plan_Teardown_EmptiesTheDeclaredSet()
    {
        // Arrange — a teardown converges towards nothing: no spelling in scope survives it.
        SqlIdentifier app = "app";
        var current = new CurrentState(_emptySchema)
        {
            Managed = new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema(app)]),
            Declared = new DefinitionSet([new ViewDefinition(ObjectAddress.View(app, "active"), "SELECT 1")]),
        };

        // Act
        var plan = Sut.Plan(current, new ProjectDefinition(new Database()), PlanningScope.To(DatabaseAddress.Schema(app))).Value!;

        // Assert
        plan.Declared.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Plan_Adopted_IsWhatTheApplyTakesOver()
    {
        // Arrange — the database already holds what the project declares, but none of it is managed yet: the
        // apply changes nothing and adopts everything.
        SqlIdentifier app = "app";
        var database = new Database { Schemas = [new Schema { Name = app, Tables = [new Table { Name = "users" }] }] };
        var plan = Sut.Plan(new CurrentState(database), new ProjectDefinition(database), PlanningScope.All).Value!;

        // Assert
        plan.Adopted.Schemas.Select(s => s.Name).ShouldBe([app]);
        plan.Adopted.SchemaObjects.ShouldBe([ObjectAddress.Table(app, "users")]);
    }

    [Fact]
    public void Plan_Adopted_ExcludesWhatIsAlreadyManaged_AndWhatTheApplyCreates()
    {
        // Arrange — `users` exists unmanaged, `orders` exists and is managed, `audit` does not exist at all.
        SqlIdentifier app = "app";
        var observed = new Database
        {
            Schemas = [new Schema { Name = app, Tables = [new Table { Name = "users" }, new Table { Name = "orders" }] }],
        };
        var current = new CurrentState(observed)
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema(app)],
                SchemaObjects: [ObjectAddress.Table(app, "orders")]),
        };
        var desired = new ProjectDefinition(new Database
        {
            Schemas =
            [
                new Schema { Name = app, Tables = [new Table { Name = "users" }, new Table { Name = "orders" }, new Table { Name = "audit" }] },
            ],
        });

        // Act
        var plan = Sut.Plan(current, desired, PlanningScope.All).Value!;

        // Assert — a created object is management the diff already shows; only the silent takeover is adoption.
        plan.Adopted.DatabaseObjects.ShouldBeEmpty();
        plan.Adopted.SchemaObjects.ShouldBe([ObjectAddress.Table(app, "users")]);
    }

    [Fact]
    public void Plan_MergesTheDifferDiagnosticsIntoTheResult()
    {
        // Arrange — run-once skips and dead-migration findings are diff-stage diagnostics; the planner surfaces them.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>())
            .Returns(Result.From(_emptyDiff, [Diagnostic.Info("data-migrations", "inert-block", "inert block")]));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("inert block");
    }

    [Fact]
    public void Plan_EnforcementAppliesToAnyPolicysFindings_NotJustTheBuiltInOnes()
    {
        // Arrange — a policy the engine knows nothing about, reporting a judgement that would block the plan.
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(Arg.Any<MigrationPlan>())
            .Returns([Diagnostic.Error("my-rules", "no-wide-tables", "too many columns")
                with { Kind = DiagnosticKind.Advisory }]);
        _planPolicies.Add(policy);
        _diagnosticOptions.ByCode["no-wide-tables"] = PolicyEnforcement.Warn;

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — downgraded, so the plan is no longer blocked by it.
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.ShouldContain(d => d.Code == "no-wide-tables");
    }

    [Fact]
    public void Plan_RunsPlanPoliciesAgainstTheCompletePlan()
    {
        // Arrange
        var diff = _emptyDiff with { DeploymentScripts = [new DeploymentScript("seed", "SELECT 1", null, DeploymentPhase.Post)] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(Arg.Is<MigrationPlan>(p => p!.Diff == diff)).Returns([Diagnostic.Error("test", "destructive", "destructive")]);
        _planPolicies.Add(policy);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — the policy received the rendered plan carrying the diff the differ produced, scripts included.
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("destructive");
        policy.Received(1).Validate(Arg.Is<MigrationPlan>(p => p!.Diff == diff));
    }

    [Fact]
    public void Plan_RendersEveryActionThroughTheDialect_ScriptsIncluded()
    {
        // Arrange — the linearizer's ordered actions render one by one through the dialect; the stub renders
        // an ExecuteScript as its verbatim Statement, carrying the transaction placement.
        var script = new DeploymentScript("seed", "INSERT INTO app.c VALUES (1);", null, DeploymentPhase.Post) { RunOutsideTransaction = true };
        _linearizer.Linearize(Arg.Any<DatabaseDiff>(), Arg.Any<PlanDependencies>(), Arg.Any<DialectCapabilities>())
            .Returns(_ => [new CreateSchema("app"), new ExecuteScript(script)]);

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.Value!.Statements.Select(s => s.Sql).ShouldBe([$"-- {nameof(CreateSchema)}", script.Sql.Value]);
        result.Value!.Statements[1].RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Plan_CarriesTheDifferDiffOnTheArtifact()
    {
        // Arrange
        var diff = _emptyDiff with { DeploymentScripts = [new DeploymentScript("seed", "SELECT 1", null, DeploymentPhase.Post)] };
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(diff, []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert
        result.Value!.Diff.ShouldBe(diff);
    }

    [Fact]
    public void Plan_WithoutADialect_Fails()
    {
        // Arrange
        var sut = new MigrationPlanner(Options.Create(new DiagnosticOptions()), _differ, _linearizer, _projectPolicies, _planPolicies, new SqlEquivalence(), dialect: null);

        // Act
        var result = sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — a dialect is required: there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().ShouldBe(PlanDiagnostics.MissingDialect);
    }

    [Fact]
    public void Plan_DeclaredObjectMatchingObservedOnlyUpToCase_Warns()
    {
        // Arrange — identifiers are case-sensitive, so "Users" beside a live "users" is a new object;
        // the near-miss is almost always a misspelled adoption, so the plan warns.
        var observed = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users" }] }] };
        var declared = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "Users" }] }] };

        // Act
        var result = Sut.Plan(new CurrentState(observed), new ProjectDefinition(declared), PlanningScope.All);

        // Assert
        result.Warnings.ShouldContain(PlanDiagnostics.CaseOnlyMismatch(
            ObjectAddress.Table("app", "Users"),
            ObjectAddress.Table("app", "users")));
    }

    [Fact]
    public void Plan_ObjectsInAContainerTheDatabaseDoesNotHave_BlocksThePlan()
    {
        // Arrange — nothing creates the schema, so the table it holds has nowhere to go.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(
            new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [TableDiff.Added("app", new Table { Name = "users" })] }]), []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — the evidence is the recorded state, which planning reads; it never contacts the database.
        var error = result.Errors.ShouldHaveSingleItem();
        error.ShouldBe(PlanDiagnostics.UndeclaredSchemaMissing(["app"]));

        var message = error.Message;
        message.ShouldContain("schema 'app'");
    }

    [Fact]
    public void Plan_ObjectsInSeveralContainersTheDatabaseDoesNotHave_NamesEachOne()
    {
        // Arrange — two schemas with nowhere to put their tables, so the one message has to list both.
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(
            new DatabaseDiff([
                SchemaDiff.Containing("app") with { Tables = [TableDiff.Added("app", new Table { Name = "users" })] },
                SchemaDiff.Containing("audit") with { Tables = [TableDiff.Added("audit", new Table { Name = "events" })] },
            ]), []));

        // Act
        var result = Sut.Plan(_current, _desired, PlanningScope.All);

        // Assert — one message naming both, read plurally rather than "schemas 'app', 'audit' ... creates it".
        var error = result.Errors.ShouldHaveSingleItem();
        error.ShouldBe(PlanDiagnostics.UndeclaredSchemaMissing(["app", "audit"]));

        var message = error.Message;
        message.ShouldContain("schemas 'app', 'audit'");
    }

    [Fact]
    public void Plan_ObjectsInAContainerTheDatabaseHas_IsFine()
    {
        // Arrange
        var observed = new CurrentState(new Database { Schemas = [new Schema { Name = "app" }] });
        _differ.Compare(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>()).Returns(Result.From(
            new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [TableDiff.Added("app", new Table { Name = "users" })] }]), []));

        // Act
        var result = Sut.Plan(observed, _desired, PlanningScope.All);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_DeclaredSchemaMatchingObservedOnlyUpToCase_Warns()
    {
        // Arrange
        var observed = new Database { Schemas = [new Schema { Name = "app" }] };
        var declared = new Database { Schemas = [new Schema { Name = "App" }] };

        // Act
        var result = Sut.Plan(new CurrentState(observed), new ProjectDefinition(declared), PlanningScope.All);

        // Assert
        result.Warnings.ShouldContain(PlanDiagnostics.CaseOnlyMismatch(DatabaseAddress.Schema("App"), DatabaseAddress.Schema("app")));
    }

    [Fact]
    public void Plan_ExactlyMatchingNames_ProduceNoCaseWarnings()
    {
        // Arrange
        var database = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users" }] }] };

        // Act
        var result = Sut.Plan(new CurrentState(database), new ProjectDefinition(database), PlanningScope.All);

        // Assert
        result.Warnings.ShouldNotContain(d => d.Message.Contains("only in case"));
    }
}
