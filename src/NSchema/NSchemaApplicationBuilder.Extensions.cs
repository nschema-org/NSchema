using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;
using NSchema.Migration.Sql;

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

    /// <summary>
    /// Replaces the default <see cref="IMigrationExecutor"/> with a custom one. Use this to target
    /// non-SQL destinations (JSON, YAML, in-memory) that do not fit the <see cref="ISqlPlanner"/> +
    /// <see cref="ISqlExecutor"/> pair.
    /// </summary>
    /// <typeparam name="T">The type of the migration executor to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseMigrationExecutor<T>() where T : class, IMigrationExecutor
    {
        Services.AddSingleton<IMigrationExecutor, T>();
        return this;
    }
}
