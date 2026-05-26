using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Policies;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
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
}
