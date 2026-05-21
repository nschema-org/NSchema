using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSchema.Migration;
using NSchema.Postgres.Current;
using NSchema.Postgres.Migration;

namespace NSchema.Postgres;

public static class NSchemaApplicationBuilderExtensions
{
    extension(NSchemaApplicationBuilder builder)
    {
        public NSchemaApplicationBuilder UsePostgres(string connectionString)
        {
            builder.Services.AddNpgsqlDataSource(connectionString);
            return builder.UsePostgres();
        }

        public NSchemaApplicationBuilder UsePostgres(Action<NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.UsePostgres();
        }

        public NSchemaApplicationBuilder UsePostgres(Action<IServiceProvider, NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.UsePostgres();
        }

        public NSchemaApplicationBuilder UsePostgres()
        {
            builder.Services
                .AddSingleton<ICurrentSchemaProvider, PostgresCurrentSchemaProvider>()
                .AddSingleton<ISchemaMigrator, PostgresSchemaMigrator>();

            return builder;
        }
    }
}
