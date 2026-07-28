using NSchema.Deployment;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;
using NSchema.Project;
using NSchema.Project.Domain.Directives;
using NSchema.State;
using NSchema.State.Domain;
using NSchema.State.Plugins;

namespace NSchema.Tests.Operations;

public sealed class MigrationWorkflowTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly IDatabaseProvider _liveDatabase = Substitute.For<IDatabaseProvider>();
    private readonly IProjectProvider _projectProvider = Substitute.For<IProjectProvider>();
    private readonly IDatabaseStateSerializer _stateSerializer = new DatabaseStateSerializer();

    private MigrationWorkflow BuildSut(IDatabaseStateStore? store) =>
        new(_planner, _progress, _liveDatabase, _projectProvider, new DatabaseStateManager(_stateSerializer, store));

    /// <summary>Planning requires a store, so the default fixture carries an empty in-memory one.</summary>
    private MigrationWorkflow BuildSut() => BuildSut(new EphemeralStateStore());

    private static Result<ProjectDefinition> ProjectDefinition(Database schema) => Result.Success(new ProjectDefinition(schema));

    private static MigrationPlan EmptyPlan() => new(new DatabaseDiff([]), []);

    /// <summary>An applied plan carrying one run-once script, so the capture has an execution to record.</summary>
    private static MigrationPlan AppliedPlan(string name, string sql) => new(
        new DatabaseDiff([]) { DeploymentScripts = [new DeploymentScript(name, sql, null, DeploymentPhase.Post) { RunCondition = RunCondition.Once }] },
        [new SqlStatement(sql)]);

    /// <summary>The hash the capture is expected to record for a script body of <paramref name="sql"/>.</summary>
    private static ScriptHash HashOf(string sql) =>
        new DeploymentScript("seed", sql, null, DeploymentPhase.Post).Hash;

    private readonly MigrationWorkflow _sut;

    public MigrationWorkflowTests()
    {
        _liveDatabase
            .GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(new Database { Schemas = [] });

        _projectProvider
            .GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(ProjectDefinition(new Database { Schemas = [] }));

        _planner.Validate(Arg.Any<ProjectDefinition>()).Returns(Result.Success());

        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.Success(EmptyPlan()));

        _sut = BuildSut();
    }

    /// <summary>Builds a workflow whose store records <paramref name="recorded"/> as the current schema.</summary>
    private MigrationWorkflow SutWithRecordedSchema(Database recorded) =>
        SutWithRecordedState(new DatabaseState(recorded));

    private MigrationWorkflow SutWithRecordedState(DatabaseState recorded)
    {
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(recorded))));
        return BuildSut(store);
    }

    [Fact]
    public async Task Validate_ReturnsNoFindings_WhenNoPolicyErrors()
    {
        // Arrange
        var desired = new Database { Schemas = [new Schema { Name = "app" }] };
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>()).Returns(ProjectDefinition(desired));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are data, not a pass/fail verdict; a clean schema yields none.
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validate_PolicyViolation_ReturnsErrorFindings_WithoutReporting()
    {
        // Arrange
        _planner.Validate(Arg.Any<ProjectDefinition>()).Returns(Result.From(Diagnostic.Error("P1", "msg")));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — the findings are carried back as data, not thrown; the workflow no longer renders them (the caller does).
        findings.IsFailure.ShouldBeTrue();
        findings.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
    }

    [Fact]
    public async Task Validate_NonErrorDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange
        _planner.Validate(Arg.Any<ProjectDefinition>())
            .Returns(Result.From(new Diagnostic("P1", "info", DiagnosticSeverity.Info)));

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert — advisories ride along in the findings rather than being reported here.
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("info");
    }

    [Fact]
    public async Task Validate_ProjectDiagnostics_AreCarriedInTheFindings()
    {
        // Arrange — findings raised while reading the DDL (e.g. deprecated syntax) arrive on the read result.
        var project = Result.From(TestProjects.Project(new Database { Schemas = [] }), [Diagnostic.Warning("deprecations", "old form")]);
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var findings = await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        findings.IsSuccess.ShouldBeTrue();
        findings.Diagnostics.ShouldHaveSingleItem().Source.ShouldBe("deprecations");
    }

    [Fact]
    public async Task Validate_DoesNotContactTheLiveDatabase()
    {
        // Act
        await _sut.Validate(TestContext.Current.CancellationToken);

        // Assert
        await _liveDatabase.DidNotReceive().GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputePlan_ReturnsComputedPlan_WithoutRendering()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([]), [new SqlStatement("CREATE SCHEMA app")]);
        _planner
            .Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.Success(plan));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — the result is returned for the caller to render; the workflow renders nothing itself.
        result.Value!.ShouldBe(plan);
    }

    [Theory]
    [InlineData(PlanTarget.Project)]
    [InlineData(PlanTarget.Empty)]
    public async Task ComputePlan_NeverTouchesTheLiveDatabase(PlanTarget target)
    {
        // Planning is always a pure state read — the current side is the recorded database, never the live one.

        // Act
        await _sut.ComputePlan(target, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        await _liveDatabase.DidNotReceive().GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
        _planner.Received(1).Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_PlansTheRecordedSchemaAsTheCurrentSide()
    {
        // Arrange
        var sut = SutWithRecordedSchema(new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users" }] }] });

        // Act
        await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — the database round-trips through the store, so match it by content, not reference.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task Prepare_ReportsVerboseObjectCensusForBothSchemas()
    {
        // Arrange
        var desired = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users" }, new Table { Name = "orders" }] }] };
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(
                TestProjects.Project(desired, [new DeploymentScript("seed", "select 1", null, DeploymentPhase.Post)])));
        var sut = SutWithRecordedSchema(new Database { Schemas = [new Schema { Name = "app" }] });

        // Act
        await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — verbose census is transient narration, emitted as Detail-level progress.
        _progress.Received().Report(OperationProgress.Detail("Declared: 1 schema, 2 tables, 1 script."));
        _progress.Received().Report(OperationProgress.Detail("Recorded state: 1 schema, 0 tables."));
    }

    [Fact]
    public async Task ComputePlan_Teardown_ReadsNoProject()
    {
        // A teardown converges on nothing, so there is no project to read.

        // Act
        await _sut.ComputePlan(PlanTarget.Empty, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        await _projectProvider.DidNotReceive().GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_PlansTowardsAnEmptySchema()
    {
        // Arrange — teardown is not a third kind of plan: it is the recorded schema diffed against nothing.
        var sut = SutWithRecordedSchema(new Database { Schemas = [new Schema { Name = "app" }] });

        // Act
        await sut.ComputePlan(PlanTarget.Empty, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app" })),
            Arg.Is<ProjectDefinition>(p => p!.Database.Schemas.Count == 0), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_ScopesTheRecordedSchema()
    {
        // Arrange — scoping is no longer special-cased; a partial teardown narrows like any other plan.
        var sut = SutWithRecordedSchema(new Database
        {
            Schemas = [new Schema { Name = "app" }, new Schema { Name = "billing" }],
        });

        // Act
        await sut.ComputePlan(PlanTarget.Empty, PlanningScope.To(new SchemaAddress("app")), TestContext.Current.CancellationToken);

        // Assert — the scope reaches the planner, which narrows the difference it computes. The current side
        // stays whole: a scoped teardown may still have to disturb what it is not tearing down.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app", "billing" })),
            Arg.Any<ProjectDefinition>(),
            Arg.Is<PlanningScope>(s => !s!.IsUnscoped && s.Addresses.Select(a => a.Value).SequenceEqual(new[] { "app" })));
    }

    [Fact]
    public async Task ComputePlan_Teardown_Unscoped_CoversEverythingRecorded()
    {
        // Arrange — an empty target declares no managed schemas, so scope derivation leaves the run unrestricted.
        var sut = SutWithRecordedSchema(new Database
        {
            Schemas = [new Schema { Name = "app" }, new Schema { Name = "billing" }],
        });

        // Act
        await sut.ComputePlan(PlanTarget.Empty, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app", "billing" })),
            Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_Teardown_RunsPolicies()
    {
        // Arrange — a teardown genuinely is destructive, so the finding is correct rather than noise. It must
        // stay possible, but it does not have to be easy.
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.From(EmptyPlan(), [Diagnostic.Error("destructive", "drops everything")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Empty, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("drops everything");
    }

    [Fact]
    public async Task ComputePlan_Teardown_WithoutStore_Fails()
    {
        // Arrange — the managed schema is the recorded state, so a teardown has nothing to read without a store.
        var sut = BuildSut(store: null);

        // Act
        var result = await sut.ComputePlan(PlanTarget.Empty, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(WorkflowDiagnostics.StoreRequiredForPlanning);
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_ArePrependedToThePlannerResult()
    {
        // Arrange — the pure planner never sees read provenance; this shell merges it into the outcome.
        var project = Result.From(TestProjects.Project(new Database { Schemas = [] }), [Diagnostic.Warning("deprecations", "old form")]);
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.From(EmptyPlan(),
                [Diagnostic.Warning("data-hazards", "hazard")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "data-hazards"]);
    }

    [Fact]
    public async Task ComputePlan_ReadDiagnostics_AreCarriedOnAPlannerFailureToo()
    {
        // Arrange
        var project = Result.From(TestProjects.Project(new Database { Schemas = [] }), [Diagnostic.Warning("deprecations", "old form")]);
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>()).Returns(project);
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.Failure<MigrationPlan>([Diagnostic.Error("P1", "blocked")]));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["deprecations", "P1"]);
    }

    private static Script SeedScript(RunCondition condition = RunCondition.Once) =>
        new DeploymentScript("seed", "SELECT 1", null, DeploymentPhase.Post) { RunCondition = condition };

    /// <summary>Builds a workflow whose store records <paramref name="executions"/> and whose DDL declares <paramref name="project"/>.</summary>
    private MigrationWorkflow SutWithState(ProjectDefinition project, params ScriptExecution[] executions)
    {
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(new DatabaseState(new Database { Schemas = [] }, executions)))));
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(project));
        return BuildSut(store);
    }

    [Fact]
    public async Task ComputePlan_TranslatesRecordedExecutionsIntoTheCurrentState()
    {
        // Arrange — the planner's "what I have" input is the schema plus the recorded executions; execution
        // records are shared script vocabulary, so the ledger passes straight through.
        var sut = SutWithState(TestProjects.Project(new Database { Schemas = [] }, [SeedScript()]),
            new ScriptExecution(new ScopedAddress(null, "seed"), "abc", DateTimeOffset.UnixEpoch));

        // Act
        await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.ExecutedScripts.Count == 1 && c.ExecutedScripts[0] == new ScriptExecution(new ScopedAddress(null, "seed"), "abc", DateTimeOffset.UnixEpoch)),
            Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_UnreadableState_IsAFailure_WithoutPlanning()
    {
        // Arrange — an unreadable ledger must fail the plan rather than read as empty: an empty ledger
        // would re-plan every recorded run-once script.
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult("not a state payload"u8.ToArray())));
        var sut = BuildSut(store);

        // Act
        var result = await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Source.ShouldBe("state");
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task ComputePlan_WithoutStore_Fails()
    {
        // Arrange — the diff is computed against CurrentState (schema + run-once ledger), so planning without
        // a store would plan against knowingly incomplete current state.
        var sut = BuildSut(store: null);

        // Act
        var result = await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(WorkflowDiagnostics.StoreRequiredForPlanning);
        _planner.DidNotReceive().Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>());
    }

    [Fact]
    public async Task Refresh_RecordsExecutedScripts()
    {
        // Arrange
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(AppliedPlan("seed", "SELECT 1"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var execution = _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem();
        execution.Script.Name.ShouldBe("seed");
        execution.Hash.ShouldBe(HashOf("SELECT 1"));
    }

    [Fact]
    public async Task Refresh_AppliedPlan_RecordsItsManagedSet()
    {
        // Arrange — an apply establishes exactly the management the plan computed, empty plans included.
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);
        var managed = new IdentitySet(Schemas: [new SchemaAddress("app")]);
        var applied = EmptyPlan() with { Managed = managed };

        // Act
        await sut.Refresh(applied, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Managed.Schemas.Select(s => s.Schema).ShouldBe(["app"]);
    }

    [Fact]
    public async Task Refresh_WithoutAnAppliedPlan_PreservesTheManagedSet()
    {
        // Arrange — a plain refresh observes; it neither adopts nor abandons anything.
        var managed = new IdentitySet(Schemas: [new SchemaAddress("app")]);
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(new DatabaseState(new Database { Schemas = [] }) with { Managed = managed }))));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Managed.Schemas.Select(s => s.Schema).ShouldBe(["app"]);
    }

    [Fact]
    public async Task Refresh_PreservesTheExistingLedger()
    {
        // Arrange — the ledger is the one part of state a capture cannot rebuild, so it must carry over.
        var existing = new ScriptExecution(new ScopedAddress(null, "api-login"), "hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(new DatabaseState(new Database { Schemas = [] }, [existing])))));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act — a plain refresh, nothing newly executed.
        await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().ShouldBe(existing);
    }

    [Fact]
    public async Task Refresh_ReRecordingAScript_ReplacesItsEntryByName()
    {
        // Arrange
        var existing = new ScriptExecution(new ScopedAddress(null, "seed"), "old-hash", DateTimeOffset.UnixEpoch);
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(new DatabaseState(new Database { Schemas = [] }, [existing])))));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        await sut.Refresh(AppliedPlan("seed", "SELECT 2"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldHaveSingleItem().Hash.ShouldBe(HashOf("SELECT 2"));
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_WithoutForce_FailsWithoutWriting()
    {
        // Arrange — replacing an unreadable payload resets the run-once ledger, so it must be asked for.
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(new byte[] { 0x7b })));
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.IsFailure.ShouldBeTrue();
        captured.Errors.ShouldContain(d => d.Message.Contains("force"));
        await store.DidNotReceive().Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_CorruptExistingState_WithForce_ReplacesAndFlagsTheResetLedger()
    {
        // Arrange — a forced refresh is the recovery path for corrupt state.
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(new byte[] { 0x7b })));
        ReadOnlyMemory<byte>? written = null;
        await store.Write(Arg.Do<ReadOnlyMemory<byte>>(m => written = m), Arg.Any<CancellationToken>());
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(force: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — the reset ledger rides the successful capture as warnings (an error would flip the result
        // to a failure), so the caller surfaces it without any extra plumbing.
        captured.ShouldNotBeNull();
        captured.IsSuccess.ShouldBeTrue();
        captured.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("ledger was reset"));
        captured.Diagnostics.ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
        _stateSerializer.Deserialize(written!.Value).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_ReadableExistingState_CarriesNoDiagnostics()
    {
        // Arrange
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        store.Read(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StoreReadResult(_stateSerializer.Serialize(new DatabaseState(new Database { Schemas = [] })))));
        var sut = BuildSut(store);

        // Act
        var captured = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.IsSuccess.ShouldBeTrue();
        captured.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputePlan_PolicyViolation_ReturnsErrors_WithoutThrowingOrReporting()
    {
        // Arrange
        var errors = new[] { Diagnostic.Error("P1", "msg") };
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.Failure<MigrationPlan>(errors));

        // Act — the failure is carried in the result, not thrown; the caller decides how to surface it.
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("msg");
    }

    [Fact]
    public async Task ComputePlan_DiffPolicyViolation_CarriesTheDiffAndErrors()
    {
        // Arrange: a diff policy (e.g. destructive-action on a dropped table) fails, so the result
        // carries errors but also the diff that triggered them — for the caller to render.
        var diff = new DatabaseDiff([]);
        var errors = new[] { Diagnostic.Error("destructive", "drops table") };
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.From(new MigrationPlan(diff, []), errors));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Diff.ShouldBe(diff);
    }

    [Fact]
    public async Task ComputePlan_CarriesNonErrorDiagnostics_WithoutReporting()
    {
        // Arrange
        var diagnostics = new[] { new Diagnostic("P1", "info", DiagnosticSeverity.Info) };
        var plan = EmptyPlan();
        _planner.Plan(Arg.Any<CurrentState>(), Arg.Any<ProjectDefinition>(), Arg.Any<PlanningScope>())
            .Returns(Result.From(plan, diagnostics));

        // Act
        var result = await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — advisories ride in the result, rendered by the caller, not reported here.
        result.Diagnostics.ShouldBe(diagnostics);
    }

    [Fact]
    public async Task Prepare_DerivesScopeFromDesiredSchema_WhenNoExplicitScope()
    {
        // Arrange
        // The derived scope covers the declared schemas plus every schema holding managed identities — a
        // schema whose declarations were all removed stays under management until its drops apply.
        var desired = new Database
        {
            Schemas = [new Schema { Name = "app" }, new Schema { Name = "admin" }],
        };
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ProjectDefinition(desired)));
        var sut = SutWithRecordedState(new DatabaseState(new Database
        {
            Schemas = [
            new Schema { Name = "app" },
            new Schema { Name = "admin" },
            new Schema { Name = "legacy" },
            new Schema { Name = "unmanaged" },
        ],
        }) with
        { Managed = new IdentitySet(Schemas: [new SchemaAddress("legacy")]) });

        // Act
        await sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — the derived scope is what keeps the diff from planning to drop the unmanaged schema.
        _planner.Received(1).Plan(
            Arg.Any<CurrentState>(),
            Arg.Any<ProjectDefinition>(),
            Arg.Is<PlanningScope>(s => !s!.IsUnscoped && s.Addresses.Select(a => a.Value).Order().SequenceEqual(new[] { "admin", "app", "legacy" })));
    }

    [Fact]
    public async Task Prepare_PassesExplicitScopeToTheProjectProvider()
    {
        // Arrange
        PlanningScope? desiredScope = null;
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<PlanningScope>(); return ProjectDefinition(new Database { Schemas = [] }); });

        // Act
        await _sut.ComputePlan(PlanTarget.Project, PlanningScope.To(new SchemaAddress("app"), new SchemaAddress("legacy")), TestContext.Current.CancellationToken);

        // Assert — the project read is load-bearing: template instances bind at aggregation.
        desiredScope!.Addresses.Select(a => a.Value).ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task Prepare_PassesTheWholeCurrentSideToThePlanner_WithTheScopeBeside()
    {
        // Arrange
        var sut = SutWithRecordedSchema(new Database
        {
            Schemas = [new Schema { Name = "app" }, new Schema { Name = "other" }],
        });

        // Act
        await sut.ComputePlan(PlanTarget.Project, PlanningScope.To(new SchemaAddress("app")), TestContext.Current.CancellationToken);

        // Assert — narrowing the current side here would hide the out-of-scope objects a scoped run may still
        // disturb, so the planner is handed everything and told what is in play.
        _planner.Received(1).Plan(
            Arg.Is<CurrentState>(c => c!.Database.Schemas.Select(s => s.Name.Value).SequenceEqual(new[] { "app", "other" })),
            Arg.Any<ProjectDefinition>(),
            Arg.Is<PlanningScope>(s => !s!.IsUnscoped && s.Addresses.Select(a => a.Value).SequenceEqual(new[] { "app" })));
    }

    [Fact]
    public async Task Prepare_PassesTheAllScopeToTheDesiredProvider_WhenNoExplicitScope()
    {
        // Arrange
        PlanningScope? desiredScope = null;
        _projectProvider.GetProject(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(call => { desiredScope = call.Arg<PlanningScope>(); return ProjectDefinition(new Database { Schemas = [] }); });

        // Act
        await _sut.ComputePlan(PlanTarget.Project, PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — the project read is unrestricted; only the current read gets the derived scope.
        desiredScope.ShouldNotBeNull();
        desiredScope!.IsUnscoped.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStore_Unscoped()
    {
        // Arrange
        var schema = new Database { Schemas = [new Schema { Name = "app" }] };
        var store = Substitute.For<IDatabaseStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(Result.Success(new StoreReadResult(null)));
        store.Write(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _liveDatabase
            .GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(schema);
        var sut = BuildSut(store);

        var expected = _stateSerializer.Serialize(new DatabaseState(schema)).ToArray();

        // Act
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        capture.ShouldNotBeNull();
        await _liveDatabase.Received(1).GetDatabase(Arg.Is<PlanningScope>(s => s!.IsUnscoped), Arg.Any<CancellationToken>());
        await store.Received(1).Write(
            Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(expected)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_NoStore_Fails_WithoutContactingTheDatabase()
    {
        // Arrange
        var sut = BuildSut(store: null);

        // Act — state is where a run is recorded, so having nowhere to record is a failure, not a no-op.
        var capture = await sut.Refresh(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: fails (never throws) and does not touch the live database.
        capture.IsFailure.ShouldBeTrue();
        capture.Errors.ShouldHaveSingleItem().Message.ShouldContain("without a configured state store");
        await _liveDatabase.DidNotReceive().GetDatabase(Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }
}
