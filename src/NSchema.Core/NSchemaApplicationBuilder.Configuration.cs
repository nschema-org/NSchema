using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Apply;
using NSchema.Diff.Backends;
using NSchema.Operations.Progress;
using NSchema.Plan.Backends;
using NSchema.Plan.Policies;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Configures the policy to apply when a destructive action is detected in the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithDestructiveActionPolicy(PolicyEnforcement policy)
    {
        Services.Configure<DestructiveActionOptions>(o => o.Policy = policy);
        return this;
    }

    /// <summary>
    /// Configures the policy to apply when the migration plan contains a change that can fail on existing data.
    /// </summary>
    public NSchemaApplicationBuilder WithDataHazardPolicy(PolicyEnforcement policy)
    {
        Services.Configure<DataHazardOptions>(o => o.Policy = policy);
        return this;
    }

    /// <summary>
    /// Configures the transaction mode to use when executing the migration plan.
    /// </summary>
    public NSchemaApplicationBuilder WithTransactionMode(TransactionMode mode)
    {
        Services.Configure<SqlOptions>(o => o.TransactionMode = mode);
        return this;
    }

    /// <summary>
    /// Sets the <see cref="SqlDialect"/> the application renders SQL with, replacing any previously set one.
    /// Typically called by a database-provider extension.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlDialect<T>() where T : SqlDialect
    {
        Services.Replace(ServiceDescriptor.Singleton<SqlDialect, T>());
        return this;
    }

    /// <summary>
    /// Sets the <see cref="SqlEquivalence"/> the application compares schemas with,
    /// replacing any previously set one. Typically called by a database-provider extension.
    /// </summary>
    public NSchemaApplicationBuilder UseSqlEquivalence<T>() where T : SqlEquivalence
    {
        Services.Replace(ServiceDescriptor.Singleton<SqlEquivalence, T>());
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter<TProgress>() where TProgress : class, IProgress<OperationProgress>
    {
        Services.Replace(ServiceDescriptor.Singleton<IProgress<OperationProgress>, TProgress>());
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter(IProgress<OperationProgress> reporter)
    {
        Services.Replace(ServiceDescriptor.Singleton(reporter));
        return this;
    }

    /// <summary>
    /// Configures the sink that receives an operation's transient progress narration.
    /// </summary>
    public NSchemaApplicationBuilder UseProgressReporter(Func<IServiceProvider, IProgress<OperationProgress>> factory)
    {
        Services.Replace(ServiceDescriptor.Singleton(factory));
        return this;
    }
}
