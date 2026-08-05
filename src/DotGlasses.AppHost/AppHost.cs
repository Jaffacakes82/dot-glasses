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

// Provisions the storage account this Bicep needs (Reference Data's ImageUrl field is still a
// plain admin-pasted URL — see CLAUDE.md's [OPEN] items; a real upload feature consuming this
// blob container is separate application-layer work, deliberately not part of this pass).
// RunAsEmulator keeps local `dotnet run` using Azurite (no real Azure Storage account touched in
// dev) while still generating real Azure Storage Bicep at publish time, same pattern as Postgres
// above.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var referenceDataImages = storage.AddBlobContainer("reference-data-images");

var web = builder.AddProject<Projects.DotGlasses_Web>("web")
    .WithComputeEnvironment(containerAppEnvironment)
    .WithReference(dotglassesdb)
    .WaitFor(dotglassesdb)
    .WithReference(referenceDataImages)
    .WaitFor(referenceDataImages);

// No Aspire hosting integration exists for Azure Communication Services (confirmed via
// `dotnet package search`, both official and CommunityToolkit) — AddBicepTemplate is the
// sanctioned way to add a custom Azure resource that still participates in `azd infra gen`'s
// regenerable pipeline, per CLAUDE.md's Deployment section. There's no local ACS emulator, and
// unlike AddAzurePostgresFlexibleServer/AddAzureStorage a raw AddBicepTemplate has no
// RunAsContainer/RunAsEmulator escape hatch — without this IsPublishMode guard, plain
// `dotnet run` would try to actually provision it against a real Azure subscription on every
// local start (and hang waiting for `az login` credentials that don't exist in dev). Gating it
// to publish-only means Development keeps using LoggingEmailSender unaffected (see Program.cs's
// IEmailSender registration) and only `azd provision`/`azd up` ever touches this resource.
if (builder.ExecutionContext.IsPublishMode)
{
    var acs = builder.AddBicepTemplate("acs", "acs.bicep");
    web.WithEnvironment("ACS_CONNECTION_STRING", acs.GetOutput("communicationServiceConnectionString"))
        .WithEnvironment("ACS_SENDER_DOMAIN", acs.GetOutput("managedDomainName"));
}

builder.Build().Run();
