using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
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
    /// Adds all concrete types that implement <see cref="IMigrationPlanTransformer"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for plan transformers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformersFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IMigrationPlanTransformer).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(IMigrationPlanTransformer), type, ServiceLifetime.Singleton));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="IMigrationPlanTransformer"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for plan transformers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddPlanTransformersFromAssemblyContaining<T>() => AddPlanTransformersFromAssembly(typeof(T).Assembly);
}
