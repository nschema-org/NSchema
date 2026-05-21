using Microsoft.Extensions.Hosting;
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;

string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                          ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSchemasFromAssemblyContaining<Program>()
    .UsePostgres(connectionString)
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var migration = builder.Build();

await migration.RunAsync();
