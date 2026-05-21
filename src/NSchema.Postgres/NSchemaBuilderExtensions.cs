using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSchema.Current;
using NSchema.Migration;
using NSchema.Postgres.Current;
using NSchema.Postgres.Migration;

namespace NSchema.Postgres;

public static class NSchemaApplicationBuilderExtensions
{
    extension(NSchemaApplicationBuilder builder)
    {
        public NSchemaApplicationBuilder UsePostgresCurrent(string connectionString)
        {
            builder.Services.AddNpgsqlDataSource(connectionString);
            return builder.AddPostgresCore();
        }

        public NSchemaApplicationBuilder UsePostgresCurrent(Action<NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.AddPostgresCore();
        }

        public NSchemaApplicationBuilder UsePostgresCurrent(Action<IServiceProvider, NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.AddPostgresCore();
        }

        private NSchemaApplicationBuilder AddPostgresCore()
        {
            builder.Services
                .AddSingleton<ICurrentSchemaProvider, PostgresCurrentSchemaProvider>()
                .AddSingleton<ISchemaMigrator, PostgresSchemaMigrator>();

            return builder;
        }
    }
}
