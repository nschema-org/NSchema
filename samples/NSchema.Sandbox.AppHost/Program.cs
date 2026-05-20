var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
{
    Args = args,
    // Disable the Aspire dashboard when running via `dotnet run`.
    // Set DOTNET_DASHBOARD_OTLP_ENDPOINT_URL to re-enable it via the IDE or aspire CLI.
    DisableDashboard = !args.Contains("--dashboard"),
});

var pgPassword = builder.AddParameter("sandbox-pg-password", "sandbox", secret: true);

var postgres = builder.AddPostgres("sandbox-postgres", password: pgPassword)
    .WithDataVolume("nschema-sandbox-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("sandbox");

builder.AddProject<Projects.NSchema_Sandbox>("migrator")
    .WithReference(db)
    .WaitFor(db);

await builder.Build().RunAsync();
