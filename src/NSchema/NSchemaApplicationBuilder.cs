using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.ScriptProviders;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema;

public class NSchemaApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    internal NSchemaApplicationBuilder(NSchemaApplicationOptions options)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>() {
            { "Logging:LogLevel:Microsoft.Hosting.Lifetime", nameof(LogLevel.Warning) }
        });

        // When left empty, the content root usually defaults to the current working directory.
        // Since NSchema will usually be run from a project/repository directory, it won't be able to
        // find things like appsettings.json.
        string contentRoot = options.ContentRootPath ?? AppContext.BaseDirectory;

        _innerBuilder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = options.Args,
            ApplicationName = options.ApplicationName,
            EnvironmentName = options.EnvironmentName,
            ContentRootPath = contentRoot,
            Configuration = configuration,
        });

        // We have our own error handling in place for this.
        _innerBuilder.Services
            .Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        _innerBuilder.Services
            .AddOptions<MigrationOptions>();
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

    public NSchemaApplicationBuilder AddSchema<T>() where T : IDesiredSchemaProvider
    {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDesiredSchemaProvider), typeof(T), ServiceLifetime.Singleton));
        if (typeof(IDeploymentScriptProvider).IsAssignableFrom(typeof(T)))
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDeploymentScriptProvider), typeof(T), ServiceLifetime.Singleton));
        return this;
    }

    public NSchemaApplicationBuilder AddSchemasFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDesiredSchemaProvider).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDesiredSchemaProvider), type, ServiceLifetime.Singleton));
            if (typeof(IDeploymentScriptProvider).IsAssignableFrom(type))
                Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDeploymentScriptProvider), type, ServiceLifetime.Singleton));
        }
        return this;
    }

    public NSchemaApplicationBuilder AddSchemasFromAssemblyContaining<T>()
        => AddSchemasFromAssembly(typeof(T).Assembly);

    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<MigrationOptions>(o => o.DestructiveActionPolicy = policy);
        return this;
    }

    public NSchemaApplicationBuilder WithDryRun(bool dryRun = true)
    {
        Services.Configure<MigrationOptions>(o => o.DryRun = dryRun);
        return this;
    }

    public NSchemaApplicationBuilder AddSchemaPolicy<T>() where T : class, ISchemaPolicy
    {
        var descriptor = new ServiceDescriptor(typeof(ISchemaPolicy), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    public NSchemaApplicationBuilder AddPlanTransformer<T>() where T : class, IMigrationPlanTransformer
    {
        var descriptor = new ServiceDescriptor(typeof(IMigrationPlanTransformer), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    public NSchemaApplicationBuilder UseSqlExecutor<T>() where T : class, ISqlExecutor
    {
        Services.AddSingleton<ISqlExecutor, T>();
        return this;
    }

    public NSchemaApplicationBuilder AddActionPolicy<T>() where T : class, IMigrationPolicy
    {
        var descriptor = new ServiceDescriptor(typeof(IMigrationPolicy), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    public NSchemaApplicationBuilder AddPreDeploymentScript(string name, string sql)
        => AddScriptProvider(new InlineScriptProvider(DeploymentPhase.Pre, new Script(name, sql)));

    public NSchemaApplicationBuilder AddPostDeploymentScript(string name, string sql)
        => AddScriptProvider(new InlineScriptProvider(DeploymentPhase.Post, new Script(name, sql)));

    public NSchemaApplicationBuilder AddPreDeploymentScriptFromFile(string path, string? name = null)
        => AddScriptProvider(new FileScriptProvider(DeploymentPhase.Pre, path, name));

    public NSchemaApplicationBuilder AddPostDeploymentScriptFromFile(string path, string? name = null)
        => AddScriptProvider(new FileScriptProvider(DeploymentPhase.Post, path, name));

    public NSchemaApplicationBuilder AddPreDeploymentScriptFromEmbeddedResource(Assembly assembly, string resourceName, string? name = null)
        => AddScriptProvider(new EmbeddedResourceScriptProvider(DeploymentPhase.Pre, assembly, resourceName, name));

    public NSchemaApplicationBuilder AddPostDeploymentScriptFromEmbeddedResource(Assembly assembly, string resourceName, string? name = null)
        => AddScriptProvider(new EmbeddedResourceScriptProvider(DeploymentPhase.Post, assembly, resourceName, name));

    public NSchemaApplicationBuilder AddPreDeploymentScriptsFromEmbeddedResources(Assembly assembly, string resourcePrefix)
        => AddScriptProvider(new EmbeddedResourcePrefixScriptProvider(DeploymentPhase.Pre, assembly, resourcePrefix));

    public NSchemaApplicationBuilder AddPostDeploymentScriptsFromEmbeddedResources(Assembly assembly, string resourcePrefix)
        => AddScriptProvider(new EmbeddedResourcePrefixScriptProvider(DeploymentPhase.Post, assembly, resourcePrefix));

    private NSchemaApplicationBuilder AddScriptProvider(IDeploymentScriptProvider provider)
    {
        Services.AddSingleton(provider);
        return this;
    }

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
    public void ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null
    ) where TContainerBuilder : notnull => _innerBuilder.ConfigureContainer(factory, configure);

    private static void ApplyServices(IServiceCollection services)
    {
        services.TryAddSingleton<ISchemaComparer, DefaultSchemaComparer>();
        services.TryAddSingleton<ISchemaAggregator, DefaultSchemaAggregator>();
        services.TryAddSingleton<ISchemaMigrator, DefaultSchemaMigrator>();
        services.TryAddSingleton<ISqlExecutor, DefaultSqlExecutor>();

        services.TryAddEnumerable(
            new ServiceDescriptor(typeof(IMigrationPlanTransformer), typeof(ActionOrderingTransformer), ServiceLifetime.Singleton));

        services.TryAddEnumerable(
            new ServiceDescriptor(typeof(IMigrationPolicy), typeof(DestructiveActionMigrationPolicy), ServiceLifetime.Singleton));

        // This is the service responsible for running the migration.
        services.AddHostedService<NSchemaHost>();
    }
}
