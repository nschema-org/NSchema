using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;


namespace NSchema.Tests.Migration;

public sealed class DefaultMigrationPlannerTests
{
    private readonly ISchemaProvider _current = Substitute.For<ISchemaProvider>();
    private readonly ISchemaProvider _desired = Substitute.For<ISchemaProvider>();
    private readonly List<ISchemaProvider> _desiredProviders;
    private readonly List<IScriptProvider> _scripts = [];
    private readonly ISchemaAggregator _aggregator = Substitute.For<ISchemaAggregator>();
    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IMigrationPlanTransformer> _transformers = [];
    private readonly List<IMigrationPolicy> _migrationPolicies = [];
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private DefaultMigrationPlanner _sut => new(
        _current,
        _desiredProviders,
        _scripts,
        _aggregator,
        _comparer,
        _schemaPolicies,
        _transformers,
        _migrationPolicies,
        _options
    );

    public DefaultMigrationPlannerTests()
    {
        _desiredProviders = [_desired];

        _current.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _desired.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(DatabaseSchema.Create([]));
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>())
            .Returns(call => call.Arg<IReadOnlyList<DatabaseSchema>>()[0]);
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([], call.ArgAt<DatabaseSchema>(1)));
    }

    private static ISchemaProvider ProviderReturning(DatabaseSchema schema, Action<string[]?>? captureScope = null)
    {
        var p = Substitute.For<ISchemaProvider>();
        p.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captureScope?.Invoke(call.Arg<string[]>());
                return Task.FromResult(schema);
            });
        return p;
    }

    [Fact]
    public async Task GetMigrationPlan_AggregatesDesiredSchemasBeforeComparing()
    {
        // Arrange
        var s1 = DatabaseSchema.Create([SchemaDefinition.Create("a")]);
        var s2 = DatabaseSchema.Create([SchemaDefinition.Create("b")]);
        var merged = DatabaseSchema.Create([SchemaDefinition.Create("a"), SchemaDefinition.Create("b")]);
        _desiredProviders.Clear();
        _desiredProviders.Add(ProviderReturning(s1));
        _desiredProviders.Add(ProviderReturning(s2));
        _aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(merged);

        // Act
        await _sut.Plan();

        // Assert
        _aggregator.Received(1).Aggregate(Arg.Is<IReadOnlyList<DatabaseSchema>>(l => l.Count == 2));
        _comparer.Received(1).Compare(Arg.Any<DatabaseSchema>(), merged);
    }

    [Fact]
    public async Task GetMigrationPlan_RunsSchemaPoliciesAndReturnsErrorDiagnostics()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "broken")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Plan.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("broken");
        result.Diagnostics[0].Severity.ShouldBe(PolicySeverity.Error);
    }

    [Fact]
    public async Task GetMigrationPlan_SchemaPolicyFailure_SkipsCurrentSchemaLookupAndComparison()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "fail")]);
        _schemaPolicies.Add(policy);

        // Act
        await _sut.Plan();

        // Assert
        await _current.DidNotReceive().GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        _comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public async Task GetMigrationPlan_PassesDeclaredAndDroppedSchemaNamesToCurrentProvider()
    {
        // Arrange
        string[]? captured = null;
        _current.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<string[]>();
                return Task.FromResult(DatabaseSchema.Create([]));
            });
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);
        _desiredProviders.Clear();
        _desiredProviders.Add(ProviderReturning(desired));

        // Act
        await _sut.Plan();

        // Assert
        captured.ShouldNotBeNull();
        captured!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetMigrationPlan_DeduplicatesSchemaNamesInScope()
    {
        // Arrange
        string[]? captured = null;
        _current.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<string[]>();
                return Task.FromResult(DatabaseSchema.Create([]));
            });
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app")],
            droppedSchemas: ["app"]);
        _desiredProviders.Clear();
        _desiredProviders.Add(ProviderReturning(desired));

        // Act
        await _sut.Plan();

        // Assert
        captured!.Length.ShouldBe(1);
        captured[0].ShouldBe("app");
    }

    [Fact]
    public async Task GetMigrationPlan_InjectsPreAndPostDeploymentScriptsAroundActions()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));
        var scripts = Substitute.For<IScriptProvider>();
        scripts.GetScripts(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Script>>([
                new Script("pre", "SELECT 1", ScriptType.PreDeployment),
                new Script("post", "SELECT 2", ScriptType.PostDeployment),
            ]));
        _scripts.Add(scripts);

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.Count.ShouldBe(3);
        result.Plan.Actions[0].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("pre");
        result.Plan.Actions[1].ShouldBe(coreAction);
        result.Plan.Actions[2].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("post");
    }

    [Fact]
    public async Task GetMigrationPlan_NoScriptProviders_DoesNotAlterPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>())
            .Returns(call => new MigrationPlan([coreAction], call.ArgAt<DatabaseSchema>(1)));

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Plan.ShouldNotBeNull();
        result.Plan.Actions.ShouldHaveSingleItem();
        result.Plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public async Task GetMigrationPlan_AppliesTransformersInRegistrationOrder()
    {
        // Arrange
        var t1 = Substitute.For<IMigrationPlanTransformer>();
        var t2 = Substitute.For<IMigrationPlanTransformer>();
        var after1 = new MigrationPlan([new CreateSchema("after1")], DatabaseSchema.Create([]));
        var after2 = new MigrationPlan([new CreateSchema("after2")], DatabaseSchema.Create([]));
        t1.Transform(Arg.Any<MigrationPlan>()).Returns(after1);
        t2.Transform(after1).Returns(after2);
        _transformers.Add(t1);
        _transformers.Add(t2);

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Plan.ShouldBe(after2);
        Received.InOrder(() =>
        {
            t1.Transform(Arg.Any<MigrationPlan>());
            t2.Transform(after1);
        });
    }

    [Fact]
    public async Task GetMigrationPlan_RunsMigrationPoliciesAgainstTransformedPlanAndReturnsErrorDiagnostics()
    {
        // Arrange
        var transformer = Substitute.For<IMigrationPlanTransformer>();
        var transformed = new MigrationPlan([new DropTable("app", "users")], DatabaseSchema.Create([]));
        transformer.Transform(Arg.Any<MigrationPlan>()).Returns(transformed);
        _transformers.Add(transformer);
        var policy = Substitute.For<IMigrationPolicy>();
        policy.Validate(transformed).Returns([new PolicyError("Test", "destructive")]);
        _migrationPolicies.Add(policy);

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(transformed);
    }

    [Fact]
    public async Task GetMigrationPlan_WhenScopeConfigured_PassesScopeToProvidersAndCurrent()
    {
        // Arrange
        string[]? desiredScope = null;
        string[]? currentScope = null;
        _current.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                currentScope = call.Arg<string[]>();
                return Task.FromResult(DatabaseSchema.Create([]));
            });
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")], droppedSchemas: ["legacy"]);
        _desiredProviders.Clear();
        _desiredProviders.Add(ProviderReturning(desired, s => desiredScope = s));
        _options.Value.SchemaNames = ["app", "legacy"];

        // Act
        await _sut.Plan();

        // Assert
        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task GetMigrationPlan_WhenNoScopeConfigured_PassesNullScopeToDesiredProviders()
    {
        // Arrange
        string[]? desiredScope = [];
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        _desiredProviders.Clear();
        _desiredProviders.Add(ProviderReturning(desired, s => desiredScope = s));

        // Act
        await _sut.Plan();

        // Assert
        desiredScope.ShouldBeNull();
    }

    [Fact]
    public async Task GetMigrationPlan_AggregatesDiagnosticsFromMultiplePolicies()
    {
        // Arrange
        var p1 = Substitute.For<ISchemaPolicy>();
        var p2 = Substitute.For<ISchemaPolicy>();
        p1.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P1", "a")]);
        p2.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P2", "b"), new PolicyError("P2", "c")]);
        _schemaPolicies.Add(p1);
        _schemaPolicies.Add(p2);

        // Act
        var result = await _sut.Plan();

        // Assert
        result.Diagnostics.Count.ShouldBe(3);
    }
}
