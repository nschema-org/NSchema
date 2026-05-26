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
}
