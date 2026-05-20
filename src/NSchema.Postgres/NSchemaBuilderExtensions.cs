using Npgsql;
using NSchema.Hosting;

namespace NSchema.Postgres;

public static class NSchemaBuilderExtensions
{
    public static NSchemaBuilder UsePostgres(
        this NSchemaBuilder builder,
        NpgsqlDataSource dataSource,
        params string[] schemaNames)
    {
        builder.UseExtractor(new PostgresSchemaExtractor(dataSource, schemaNames));
        builder.UseExecutor(new PostgresInstructionExecutor(dataSource));
        return builder;
    }
}
