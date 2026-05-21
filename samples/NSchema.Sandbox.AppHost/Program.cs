var builder = DistributedApplication.CreateBuilder();

var pgPassword = builder
    .AddParameter("sandbox-pg-password", "sandbox", secret: true);

var postgres = builder.AddPostgres("sandbox-postgres", password: pgPassword)
    .WithDataVolume("nschema-sandbox-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("sandbox");

builder.AddProject<Projects.NSchema_Sandbox>("migrator")
    .WithEnvironment("CONNECTION_STRING", db)
    .WaitFor(db);

await builder.Build().RunAsync();
