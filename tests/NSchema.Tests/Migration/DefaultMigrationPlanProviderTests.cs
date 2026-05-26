using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public sealed class DefaultMigrationPlanProviderTests
{
    private static ISchemaProvider DesiredProvider(DatabaseSchema schema, Action<string[]?>? captureScope = null)
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

    private static ICurrentSchemaProvider CurrentProvider(DatabaseSchema schema, Action<string[]?>? captureScope = null)
    {
        var p = Substitute.For<ICurrentSchemaProvider>();
        p.GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captureScope?.Invoke(call.Arg<string[]>());
                return Task.FromResult(schema);
            });
        return p;
    }

    private static ISchemaAggregator PassThroughAggregator()
    {
        var a = Substitute.For<ISchemaAggregator>();
        a.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>())
            .Returns(call => call.Arg<IReadOnlyList<DatabaseSchema>>()[0]);
        return a;
    }

    private static ISchemaComparer Comparer(params MigrationAction[] actions)
    {
        var c = Substitute.For<ISchemaComparer>();
        c.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(new MigrationPlan(actions));
        return c;
    }

    private static DefaultMigrationPlanProvider Build(
        ICurrentSchemaProvider? current = null,
        IEnumerable<ISchemaProvider>? desired = null,
        IEnumerable<IScriptProvider>? scripts = null,
        ISchemaAggregator? aggregator = null,
        ISchemaComparer? comparer = null,
        IEnumerable<ISchemaPolicy>? schemaPolicies = null,
        IEnumerable<IMigrationPlanTransformer>? transformers = null,
        IEnumerable<IMigrationPolicy>? migrationPolicies = null,
        MigrationOptions? options = null
    ) => new(
        Substitute.For<IMigrationReporter>(),
        current ?? CurrentProvider(DatabaseSchema.Create([])),
        desired ?? [DesiredProvider(DatabaseSchema.Create([]))],
        scripts ?? [],
        aggregator ?? PassThroughAggregator(),
        comparer ?? Comparer(),
        schemaPolicies ?? [],
        transformers ?? [],
        migrationPolicies ?? [],
        Options.Create(options ?? new MigrationOptions())
    );

    [Fact]
    public async Task GetMigrationPlan_AggregatesDesiredSchemasBeforeComparing()
    {
        var s1 = DatabaseSchema.Create([SchemaDefinition.Create("a")]);
        var s2 = DatabaseSchema.Create([SchemaDefinition.Create("b")]);
        var aggregator = Substitute.For<ISchemaAggregator>();
        var merged = DatabaseSchema.Create([SchemaDefinition.Create("a"), SchemaDefinition.Create("b")]);
        aggregator.Aggregate(Arg.Any<IReadOnlyList<DatabaseSchema>>()).Returns(merged);
        var comparer = Comparer();

        var sut = Build(
            desired: [DesiredProvider(s1), DesiredProvider(s2)],
            aggregator: aggregator,
            comparer: comparer);

        await sut.ComputeMigrationPlan();

        aggregator.Received(1).Aggregate(Arg.Is<IReadOnlyList<DatabaseSchema>>(l => l.Count == 2));
        comparer.Received(1).Compare(Arg.Any<DatabaseSchema>(), merged);
    }

    [Fact]
    public async Task GetMigrationPlan_RunsSchemaPoliciesAndThrowsOnError()
    {
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "broken")]);

        var sut = Build(schemaPolicies: [policy]);

        var ex = await Should.ThrowAsync<PolicyViolationException>(() => sut.ComputeMigrationPlan());
        ex.Errors.ShouldHaveSingleItem();
        ex.Errors[0].Message.ShouldBe("broken");
    }

    [Fact]
    public async Task GetMigrationPlan_SchemaPolicyFailure_SkipsCurrentSchemaLookupAndComparison()
    {
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("Test", "fail")]);
        var current = CurrentProvider(DatabaseSchema.Create([]));
        var comparer = Comparer();

        var sut = Build(current: current, comparer: comparer, schemaPolicies: [policy]);

        await Should.ThrowAsync<PolicyViolationException>(() => sut.ComputeMigrationPlan());
        await current.DidNotReceive().GetSchema(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public async Task GetMigrationPlan_PassesDeclaredAndDroppedSchemaNamesToCurrentProvider()
    {
        string[]? captured = null;
        var current = CurrentProvider(DatabaseSchema.Create([]), s => captured = s);
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app"), SchemaDefinition.Create("admin")],
            droppedSchemas: ["legacy"]);

        var sut = Build(current: current, desired: [DesiredProvider(desired)]);

        await sut.ComputeMigrationPlan();

        captured.ShouldNotBeNull();
        captured!.ShouldBe(["app", "admin", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetMigrationPlan_DeduplicatesSchemaNamesInScope()
    {
        string[]? captured = null;
        var current = CurrentProvider(DatabaseSchema.Create([]), s => captured = s);
        var desired = DatabaseSchema.Create(
            [SchemaDefinition.Create("app")],
            droppedSchemas: ["app"]);

        var sut = Build(current: current, desired: [DesiredProvider(desired)]);

        await sut.ComputeMigrationPlan();

        captured!.Length.ShouldBe(1);
        captured[0].ShouldBe("app");
    }

    [Fact]
    public async Task GetMigrationPlan_InjectsPreAndPostDeploymentScriptsAroundActions()
    {
        var coreAction = new CreateSchema("app");
        var scripts = Substitute.For<IScriptProvider>();
        scripts.GetScripts(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Script>>([
                new Script("pre", "SELECT 1", ScriptType.PreDeployment),
                new Script("post", "SELECT 2", ScriptType.PostDeployment),
            ]));

        var sut = Build(scripts: [scripts], comparer: Comparer(coreAction));

        var plan = await sut.ComputeMigrationPlan();

        plan.Actions.Count.ShouldBe(3);
        plan.Actions[0].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("pre");
        plan.Actions[1].ShouldBe(coreAction);
        plan.Actions[2].ShouldBeOfType<RunScript>().Script.Name.ShouldBe("post");
    }

    [Fact]
    public async Task GetMigrationPlan_NoScriptProviders_DoesNotAlterPlan()
    {
        var coreAction = new CreateSchema("app");
        var sut = Build(comparer: Comparer(coreAction));

        var plan = await sut.ComputeMigrationPlan();

        plan.Actions.ShouldHaveSingleItem();
        plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public async Task GetMigrationPlan_AppliesTransformersInRegistrationOrder()
    {
        var t1 = Substitute.For<IMigrationPlanTransformer>();
        var t2 = Substitute.For<IMigrationPlanTransformer>();
        var after1 = new MigrationPlan([new CreateSchema("after1")]);
        var after2 = new MigrationPlan([new CreateSchema("after2")]);
        t1.Transform(Arg.Any<MigrationPlan>()).Returns(after1);
        t2.Transform(after1).Returns(after2);

        var sut = Build(transformers: [t1, t2]);

        var plan = await sut.ComputeMigrationPlan();

        plan.ShouldBe(after2);
        Received.InOrder(() =>
        {
            t1.Transform(Arg.Any<MigrationPlan>());
            t2.Transform(after1);
        });
    }

    [Fact]
    public async Task GetMigrationPlan_RunsMigrationPoliciesAgainstTransformedPlanAndThrowsOnError()
    {
        var transformer = Substitute.For<IMigrationPlanTransformer>();
        var transformed = new MigrationPlan([new DropTable("app", "users")]);
        transformer.Transform(Arg.Any<MigrationPlan>()).Returns(transformed);

        var policy = Substitute.For<IMigrationPolicy>();
        policy.Validate(transformed).Returns([new PolicyError("Test", "destructive")]);

        var sut = Build(transformers: [transformer], migrationPolicies: [policy]);

        var ex = await Should.ThrowAsync<PolicyViolationException>(() => sut.ComputeMigrationPlan());
        ex.Errors[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(transformed);
    }

    [Fact]
    public async Task GetMigrationPlan_WhenScopeConfigured_PassesScopeToProvidersAndCurrent()
    {
        string[]? desiredScope = null;
        string[]? currentScope = null;
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")], droppedSchemas: ["legacy"]);
        var current = CurrentProvider(DatabaseSchema.Create([]), s => currentScope = s);

        var sut = Build(
            current: current,
            desired: [DesiredProvider(desired, s => desiredScope = s)],
            options: new MigrationOptions { SchemaNames = ["app", "legacy"] });

        await sut.ComputeMigrationPlan();

        desiredScope.ShouldBe(["app", "legacy"]);
        currentScope.ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task GetMigrationPlan_WhenNoScopeConfigured_PassesNullScopeToDesiredProviders()
    {
        string[]? desiredScope = [];
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")]);

        var sut = Build(desired: [DesiredProvider(desired, s => desiredScope = s)]);

        await sut.ComputeMigrationPlan();

        desiredScope.ShouldBeNull();
    }

    [Fact]
    public async Task GetMigrationPlan_AggregatesErrorsFromMultiplePolicies()
    {
        var p1 = Substitute.For<ISchemaPolicy>();
        var p2 = Substitute.For<ISchemaPolicy>();
        p1.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P1", "a")]);
        p2.Validate(Arg.Any<DatabaseSchema>()).Returns([new PolicyError("P2", "b"), new PolicyError("P2", "c")]);

        var sut = Build(schemaPolicies: [p1, p2]);

        var ex = await Should.ThrowAsync<PolicyViolationException>(() => sut.ComputeMigrationPlan());
        ex.Errors.Count.ShouldBe(3);
    }
}
