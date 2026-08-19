using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Apply;
using NSchema.Deployment;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.PlanFile;
using NSchema.Plan.Policies;
using NSchema.Project;
using NSchema.Project.Policies;
using NSchema.State;
using NSchema.State.Locks;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema;

/// <summary>
/// A builder for configuring and creating an <see cref="NSchemaApplication"/>.
/// </summary>
public partial class NSchemaApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    internal NSchemaApplicationBuilder(NSchemaApplicationOptions options)
    {
        // When left empty, the content root usually defaults to the current working directory.
        // Since NSchema will usually be run from a project/repository directory, it won't be able to
        // find things like appsettings.json.
        var contentRoot = options.ContentRootPath ?? AppContext.BaseDirectory;

        _innerBuilder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = options.Args,
            ApplicationName = options.ApplicationName,
            EnvironmentName = options.EnvironmentName,
            ContentRootPath = contentRoot,
            Configuration = new ConfigurationManager(),
        });

        // Drop the default console logger so third-party libraries don't spam the terminal.
        _innerBuilder.Logging.ClearProviders();
        _innerBuilder.Services.AddOptions<DiagnosticOptions>();

        // Policies registered up front so users can remove them before Build().
        AddProjectPolicy<StructuralIntegrityPolicy>();
        AddProjectPolicy<SchemaLintPolicy>();
        AddPlanPolicy<DestructiveActionPolicy>();
        AddPlanPolicy<DataHazardPolicy>();
        AddPlanPolicy<EnumValueRemovalPolicy>();
        AddPlanPolicy<AdoptionPolicy>();
        AddPlanPolicy<CommentStoragePolicy>();
        AddPlanPolicy<ClusteringPolicy>();
        AddPlanPolicy<ReferentialActionPolicy>();
        AddPlanPolicy<GeneratedColumnStoragePolicy>();
        AddPlanPolicy<RowGuidPolicy>();
        AddPlanPolicy<NamedDefaultPolicy>();
        AddPlanPolicy<NotForReplicationPolicy>();
    }

    /// <inheritdoc />
    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)_innerBuilder).Properties;

    /// <inheritdoc />
    public IConfigurationManager Configuration => _innerBuilder.Configuration;

    /// <inheritdoc />
    public IHostEnvironment Environment => _innerBuilder.Environment;

    /// <inheritdoc />
    public ILoggingBuilder Logging => _innerBuilder.Logging;

    /// <inheritdoc />
    public IMetricsBuilder Metrics => _innerBuilder.Metrics;

    /// <inheritdoc />
    public IServiceCollection Services => _innerBuilder.Services;

    /// <summary>
    /// Builds the <see cref="NSchemaApplication" />.
    /// </summary>
    /// <returns>The configured application.</returns>
    public NSchemaApplication Build()
    {
        // We add services here to give the user a chance to supply their own before building the application.
        ApplyServices(Services);

        var host = _innerBuilder.Build();
        return new NSchemaApplication(host);
    }

    /// <inheritdoc />
    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull => _innerBuilder.ConfigureContainer(factory, configure);

    private static void ApplyServices(IServiceCollection services)
    {
        // Diffing
        services.TryAddSingleton<SqlEquivalence>();
        services.TryAddSingleton<IDatabaseComparer, DatabaseComparer>();
        services.TryAddSingleton<IProjectComparer, ProjectComparer>();

        // Operations
        services.TryAddSingleton<IMigrationWorkflow, MigrationWorkflow>();
        services.TryAddSingleton<IProgress<OperationProgress>, NullOperationProgress>();
        services.TryAddSingleton<IOperation<PlanArguments, Result<PlanResult>>, PlanOperation>();
        services.TryAddSingleton<IOperation<ApplyArguments, Result<ApplyResult>>, ApplyOperation>();
        services.TryAddSingleton<IOperation<RefreshArguments, Result<RefreshResult>>, RefreshOperation>();
        services.TryAddSingleton<IOperation<ValidateArguments, Result<ValidateResult>>, ValidateOperation>();
        services.TryAddSingleton<IOperation<DriftArguments, Result<DriftResult>>, DriftOperation>();
        services.TryAddSingleton<IOperation<ImportArguments, Result<ImportResult>>, ImportOperation>();
        services.TryAddSingleton<IOperation<DoctorArguments, Result<DoctorResult>>, DoctorOperation>();
        services.TryAddSingleton<INSchemaOperations, NSchemaOperations>();

        // Plan
        services.TryAddSingleton<IPlanLinearizer, PlanLinearizer>();
        services.TryAddSingleton<IMigrationPlanner, MigrationPlanner>();
        services.TryAddSingleton<IPlanFileManager, PlanFileManager>();

        // Schemas
        services.TryAddSingleton<IDatabaseProvider, DatabaseProvider>();
        services.TryAddSingleton<IProjectProvider, ProjectProvider>();

        // SQL
        services.TryAddSingleton<ISqlExecutor, SqlExecutor>();

        // State
        services.TryAddSingleton<IDatabaseStateSerializer, DatabaseStateSerializer>();
        services.TryAddSingleton<IDatabaseStateManager, DatabaseStateManager>();
        services.TryAddSingleton<IStateLockManager, StateLockManager>();
    }
}
