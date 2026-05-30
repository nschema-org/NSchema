using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider to the application that will be used to retrieve the desired schema.
    /// </summary>
    /// <typeparam name="T">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchema<T>() where T : ISchemaProvider
    {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(ISchemaProvider), typeof(T), ServiceLifetime.Singleton));
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="ISchemaProvider"/> to the application from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for schema providers.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemasFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ISchemaProvider).IsAssignableFrom(t));
        foreach (var type in types)
        {
            Services.TryAddEnumerable(new ServiceDescriptor(typeof(ISchemaProvider), type, ServiceLifetime.Singleton));
        }
        return this;
    }

    /// <summary>
    /// Adds all concrete types that implement <see cref="ISchemaProvider"/> to the application from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose containing assembly will be scanned for schema providers.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddSchemasFromAssemblyContaining<T>() => AddSchemasFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Registers the <see cref="ISchemaProvider"/> that supplies the current (live) database schema.
    /// </summary>
    /// <typeparam name="T">The type of the provider to register as the current-state source.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseCurrentSchema<T>() where T : class, ISchemaProvider
    {
        Services.AddKeyedSingleton<ISchemaProvider, T>(ISchemaProvider.LiveCurrentSchemaProviderKey);
        return this;
    }
}
