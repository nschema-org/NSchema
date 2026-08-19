using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Plan.Policies;
using NSchema.Project.Policies;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a policy to the application that will be used to validate the declared project.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddProjectPolicy<T>() where T : class, IProjectPolicy
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProjectPolicy, T>());
        return this;
    }

    /// <summary>
    /// Adds a policy to the application that will be used to validate the complete plan before it is executed.
    /// </summary>
    /// <typeparam name="T">The type of the policy to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanPolicy<T>() where T : class, IPlanPolicy
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPlanPolicy, T>());
        return this;
    }
}
