using System.Reflection;
using Microsoft.Extensions.Hosting;
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;

string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                          ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");

var assembly = Assembly.GetExecutingAssembly();

var builder = NSchemaApplication.CreateBuilder(args);

builder
    .AddSchemasFromAssemblyContaining<Program>()
    .AddPreDeploymentScriptsFromEmbeddedResources(assembly, "NSchema.Sandbox.Scripts.PreDeployment.")
    .UsePostgres(connectionString)
    //.WithDryRun(true)
    .WithDestructiveActionPolicy(DestructiveActionPolicy.Warn);

var migration = builder.Build();

await migration.RunAsync();
