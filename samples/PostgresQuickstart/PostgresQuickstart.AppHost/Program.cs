var builder = DistributedApplication.CreateBuilder();

var pgPassword = builder
    .AddParameter("pg-password", "quickstart", secret: true);

var postgres = builder.AddPostgres("nschema-postgres", password: pgPassword)
    .WithDataVolume("nschema-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("quickstart");

builder.AddProject<Projects.PostgresQuickstart>("migrator")
    .WithEnvironment("CONNECTION_STRING", db)
    .WaitFor(db);

await builder.Build().RunAsync();
