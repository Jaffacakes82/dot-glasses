var builder = DistributedApplication.CreateBuilder(args);

// Deploy target for compute resources (web). Explicit since Aspire 9.4 dropped "hybrid" mode,
// where azd used to silently create/own this environment on the app's behalf — see
// https://learn.microsoft.com/en-us/dotnet/aspire/compatibility/9.4/hybrid-compute-support-dropped.
var containerAppEnvironment = builder.AddAzureContainerAppEnvironment("env");

// Pinned rather than left to Aspire's auto-generate-and-cache-in-user-secrets default: that
// default is keyed off the resource's *shape* (AddPostgres vs. AddAzurePostgresFlexibleServer
// RunAsContainer), so switching between them — as this file just did — can silently regenerate
// a new value that no longer matches whatever password the persisted .WithDataVolume() volume
// was initialized with, and Postgres refuses the connection. Declaring it explicitly by name
// keeps it stable across that kind of change. No value is hardcoded here: AddParameter reads it
// from configuration (Parameters:postgres-password), which for local dev resolves from this
// project's user secrets (see UserSecretsId in DotGlasses.AppHost.csproj) — the standard .NET
// mechanism for a per-developer secret that must never live in a committed file. If unset,
// Aspire generates a value on first run and persists it there for you.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// RunAsContainer keeps local `dotnet run` exactly as before (a Postgres container with a data
// volume and pgAdmin) while making this same declaration generate Azure Database for PostgreSQL
// Flexible Server Bicep at publish time — confirmed as the intended production target.
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithPgAdmin()
        .WithPassword(postgresPassword));

var dotglassesdb = postgres.AddDatabase("dotglassesdb");

builder.AddProject<Projects.DotGlasses_Web>("web")
    .WithComputeEnvironment(containerAppEnvironment)
    .WithReference(dotglassesdb)
    .WaitFor(dotglassesdb);

builder.Build().Run();
