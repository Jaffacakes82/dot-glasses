var builder = DistributedApplication.CreateBuilder(args);

// Local dev only: Postgres runs in a container, auto-provisioned by Aspire. At deploy time,
// azd reads this model to generate Bicep for Azure Database for PostgreSQL Flexible Server —
// confirm that's still the intended target (as opposed to Azure SQL) before provisioning.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var dotglassesdb = postgres.AddDatabase("dotglassesdb");

builder.AddProject<Projects.DotGlasses_Web>("web")
    .WithReference(dotglassesdb)
    .WaitFor(dotglassesdb);

builder.Build().Run();
