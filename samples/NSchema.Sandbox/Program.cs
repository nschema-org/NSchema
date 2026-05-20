using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSchema.Migration;
using NSchema.Migration.Comparison;
using NSchema.Migration.Execution;
using NSchema.Migration.Extraction;
using NSchema.Postgres;
using NSchema.Sandbox;

string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__sandbox")
                          ?? throw new InvalidOperationException("Connection string not found in environment variables.");
var dataSource = NpgsqlDataSource.Create(connectionString);

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
    .AddSingleton(dataSource)
    .AddSingleton<ISchemaMigrator, DefaultSchemaMigrator>()
    .AddSingleton<ISchemaComparer, DefaultSchemaComparer>()
    .AddSingleton<ISchemaExtractor, PostgresSchemaExtractor>()
    .AddSingleton<IInstructionExecutor, PostgresInstructionExecutor>()
    .BuildServiceProvider();

var desired = Database.GetModel();
var migrator = services.GetRequiredService<ISchemaMigrator>();

var plan = await migrator.Plan(desired);
var options = new ExecutionOptions(DestructiveActionPolicy.Warn);
await migrator.Apply(plan, options);
