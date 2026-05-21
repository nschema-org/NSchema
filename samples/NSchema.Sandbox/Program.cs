using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;
using NSchema.Sandbox;

string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                          ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");

var builder = NSchemaApplication.CreateBuilder(args);

builder.Services
    .Configure<MigrationOptions>(o => o.DestructiveActionPolicy = DestructiveActionPolicy.Warn);

builder
    .AddSchema<BooksSchema>()
    .UsePostgresSource(connectionString);

var migration = builder.Build();

await migration.RunAsync();
