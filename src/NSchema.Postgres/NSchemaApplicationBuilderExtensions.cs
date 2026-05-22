using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSchema.Migration;
using NSchema.Postgres.Migration;

namespace NSchema.Postgres;

/// <summary>
/// Provides extension methods for configuring NSchema to use PostgreSQL as the underlying database provider.
/// </summary>
public static class NSchemaApplicationBuilderExtensions
{
    extension(NSchemaApplicationBuilder builder)
    {
        /// <summary>
        /// Configures NSchema to use PostgreSQL as the database provider with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to the PostgreSQL database.</param>
        /// <returns>The <see cref="NSchemaApplicationBuilder"/> instance, allowing for method chaining.</returns>
        public NSchemaApplicationBuilder UsePostgres(string connectionString)
        {
            builder.Services.AddNpgsqlDataSource(connectionString);
            return builder.UsePostgres();
        }

        /// <summary>
        /// Configures NSchema to use PostgreSQL as the database provider with a custom configuration action for the NpgsqlDataSourceBuilder.
        /// </summary>
        /// <param name="configure">A delegate that can be used to configure the NpgsqlDataSourceBuilder.</param>
        /// <returns>The <see cref="NSchemaApplicationBuilder"/> instance, allowing for method chaining.</returns>
        public NSchemaApplicationBuilder UsePostgres(Action<NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.UsePostgres();
        }

        /// <summary>
        /// Configures NSchema to use PostgreSQL as the database provider with a custom configuration action for the NpgsqlDataSourceBuilder that has access to the IServiceProvider.
        /// </summary>
        /// <param name="configure">A delegate that can be used to configure the NpgsqlDataSourceBuilder.</param>
        /// <returns>The <see cref="NSchemaApplicationBuilder"/> instance, allowing for method chaining.</returns>
        public NSchemaApplicationBuilder UsePostgres(Action<IServiceProvider, NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.UsePostgres();
        }

        /// <summary>
        /// Configures NSchema to use PostgreSQL as the database provider by registering the necessary services for schema management and SQL planning specific to PostgreSQL.
        /// </summary>
        /// <returns></returns>
        public NSchemaApplicationBuilder UsePostgres()
        {
            builder.Services
                .AddSingleton<ICurrentSchemaProvider, PostgresSchemaProvider>()
                .AddSingleton<ISqlPlanner, PostgresSqlPlanner>();

            return builder;
        }
    }
}
