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

/// <summary>
/// A builder for configuring and creating an <see cref="NSchemaApplication"/>.
/// </summary>
public class NSchemaApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    internal NSchemaApplicationBuilder(NSchemaApplicationOptions options)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> {
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

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve the desired schema.
    /// </summary>
    /// <typeparam name="T">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchema<T>() where T : IDesiredSchemaProvider
    {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDesiredSchemaProvider), typeof(T), ServiceLifetime.Singleton));
        if (typeof(IDeploymentScriptProvider).IsAssignableFrom(typeof(T)))
        {
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDeploymentScriptProvider), typeof(T), ServiceLifetime.Singleton));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDesiredSchemaProvider"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for schema providers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemasFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDesiredSchemaProvider).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDesiredSchemaProvider), type, ServiceLifetime.Singleton));
            if (typeof(IDeploymentScriptProvider).IsAssignableFrom(type))
            {
                Services.TryAddEnumerable(new ServiceDescriptor(typeof(IDeploymentScriptProvider), type, ServiceLifetime.Singleton));
            }
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IDesiredSchemaProvider"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for schema providers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemasFromAssemblyContaining<T>() => AddSchemasFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(DestructiveActionPolicy policy)
    {
        Services.Configure<MigrationOptions>(o => o.DestructiveActionPolicy = policy);
        return this;
    }

    /// <summary>
    /// Configures the application to perform a dry run, where the migration plan will be generated and logged but not executed against the database.
    /// </summary>
    /// <param name="dryRun">Whether to enable dry run mode. Defaults to true.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder WithDryRun(bool dryRun = true)
    {
        Services.Configure<MigrationOptions>(o => o.DryRun = dryRun);
        return this;
    }

    /// <summary>
    /// Adds a policy to the application that will be used to validate the desired schema.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemaPolicy<T>() where T : class, ISchemaPolicy
    {
        var descriptor = new ServiceDescriptor(typeof(ISchemaPolicy), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    /// <summary>
    /// Adds a transformer to the application that will be used to transform the migration plan before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the transformer to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformer<T>() where T : class, IMigrationPlanTransformer
    {
        var descriptor = new ServiceDescriptor(typeof(IMigrationPlanTransformer), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    /// <summary>
    /// Adds a custom SQL executor to the application that will be used to execute the generated migration scripts against the database.
    /// </summary>
    /// <typeparam name="T">The type of the SQL executor to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSqlExecutor<T>() where T : class, ISqlExecutor
    {
        Services.AddSingleton<ISqlExecutor, T>();
        return this;
    }

    /// <summary>
    /// Adds a policy to the application that will be used to validate the generated migration plan before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddMigrationPolicy<T>() where T : class, IMigrationPolicy
    {
        var descriptor = new ServiceDescriptor(typeof(IMigrationPolicy), typeof(T), ServiceLifetime.Singleton);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScriptProvider(IDeploymentScriptProvider provider)
    {
        Services.AddSingleton(provider);
        return this;
    }

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <typeparam name="TProvider">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScriptProvider<TProvider>() where  TProvider : class, IDeploymentScriptProvider
    {
        Services.AddSingleton<IDeploymentScriptProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Adds a SQL script to the application from a file that will be run before any other migration actions.
    /// </summary>
    /// <param name="path">The path to the SQL script file.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the file name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPreDeploymentScriptFromFile(string path, string? name = null)
        => AddScriptProvider(new FileScriptProvider(DeploymentPhase.Pre, path, name));

    /// <summary>
    /// Adds a SQL script to the application from a file that will be run after all other migration actions.
    /// </summary>
    /// <param name="path">The path to the SQL script file.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the file name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPostDeploymentScriptFromFile(string path, string? name = null)
        => AddScriptProvider(new FileScriptProvider(DeploymentPhase.Post, path, name));

    /// <summary>
    /// Adds SQL scripts to the application from files in a directory that will be run before any other migration actions. The scripts will be run in alphabetical order.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="resourceName">The name of the embedded resource containing the SQL script.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the resource name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPreDeploymentScriptFromEmbeddedResource(Assembly assembly, string resourceName, string? name = null)
        => AddScriptProvider(new EmbeddedResourceScriptProvider(DeploymentPhase.Pre, assembly, resourceName, name));

    /// <summary>
    /// Adds SQL scripts to the application from files in a directory that will be run after all other migration actions. The scripts will be run in alphabetical order.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="resourceName">The name of the embedded resource containing the SQL script.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the resource name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPostDeploymentScriptFromEmbeddedResource(Assembly assembly, string resourceName, string? name = null)
        => AddScriptProvider(new EmbeddedResourceScriptProvider(DeploymentPhase.Post, assembly, resourceName, name));

    /// <summary>
    /// Adds SQL scripts to the application from embedded resources in an assembly that will be run before any other migration actions. The scripts will be run in alphabetical order.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resources.</param>
    /// <param name="resourcePrefix">The prefix of the embedded resources to include as scripts. For example, if the assembly contains embedded resources "Scripts.Pre.Script1.sql" and "Scripts.Pre.Script2.sql", a prefix of "Scripts.Pre." would include both of these as scripts.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPreDeploymentScriptsFromEmbeddedResources(Assembly assembly, string resourcePrefix)
        => AddScriptProvider(new EmbeddedResourcePrefixScriptProvider(DeploymentPhase.Pre, assembly, resourcePrefix));

    public NSchemaApplicationBuilder AddPostDeploymentScriptsFromEmbeddedResources(Assembly assembly, string resourcePrefix)
        => AddScriptProvider(new EmbeddedResourcePrefixScriptProvider(DeploymentPhase.Post, assembly, resourcePrefix));

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
