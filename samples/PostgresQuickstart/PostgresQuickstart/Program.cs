using System.Reflection;
using Microsoft.Extensions.Hosting;
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;
using NSchema.Schema;

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                          ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");

var assembly = Assembly.GetExecutingAssembly();

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSchemasFromAssemblyContaining<Program>()
    .AddScriptsFromEmbeddedResources(ScriptType.PreDeployment, assembly, "PostgresQuickstart.Scripts.PreDeployment.")
    .UsePostgres(connectionString)
    .DryRunOnly()
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var migration = builder.Build();

await migration.RunAsync();
