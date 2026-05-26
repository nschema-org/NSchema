using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
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
    /// Registers the <see cref="ISqlPlanner"/> that generates the SQL for a migration plan.
    /// </summary>
    /// <typeparam name="T">The type of the provider to register as the current-state source.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSqlPlanner<T>() where T : class, ISqlPlanner
    {
        Services.AddSingleton<ISqlPlanner, T>();
        return this;
    }
}
