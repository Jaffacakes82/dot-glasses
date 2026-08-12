using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
using Azure.Provisioning.PostgreSql;
using Azure.Provisioning.Roles;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

// Azure CAF naming convention (https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/
// ready/azure-best-practices/resource-naming), decided with the user 2026-08-06: one
// subscription, two resource groups (rg-dotglasses-nonprod / rg-dotglasses-prod, i.e. the azd
// environment itself is named "dotglasses-nonprod"/"dotglasses-prod" — main.bicep's own
// `rg-${environmentName}` already gives the resource-group name for free from that). Every
// other resource name below is derived from `resourceGroup().name` at Bicep-evaluation time via
// EnvToken(...) rather than threaded down as a new parameter from main.bicep — it works
// unmodified in any resource-group-scoped module (every resource here is `scope: rg`) with zero
// extra plumbing, and survives `azd infra gen --force` regeneration since the expression lives
// in this C# file, not in generated Bicep. Globally-unique resource types (Storage, ACS) still
// append a short uniqueString() hash after the CAF-pattern name — CAF's own examples don't
// solve global uniqueness either, and a plain deterministic name risks colliding with someone
// else's resource anywhere on Azure.
const string Workload = "dotglasses";

// BicepFunction has no Substring/skip wrapper in this Azure.Provisioning version (confirmed via
// reflection — only Take(), which returns the first N characters, exists) — built by hand via
// the same FunctionCallExpression escape hatch Azure.Provisioning itself uses internally to
// implement its other BicepFunction.* wrappers, to emit Bicep's substring(value, startIndex).
static BicepValue<string> Substring(BicepValue<string> value, int startIndex) =>
    new(new FunctionCallExpression(
        new IdentifierExpression("substring"),
        [((IBicepValue)value).Expression!, new IntLiteralExpression(startIndex)]));

static BicepValue<string> EnvToken() =>
    Substring(BicepFunction.GetResourceGroup().Name, $"rg-{Workload}-".Length);

static BicepValue<string> ShortHash(int length = 6) =>
    BicepFunction.Take(BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id), length);

// Storage accounts and Container Registries can't contain hyphens and cap out at 24/50 chars —
// a shorter, no-separator workload token keeps "st"/"cr" + workload + env + hash comfortably
// under Storage's tighter 24-char limit (the binding one) with room to spare.
const string ShortWorkload = "dg";

// Deploy target for compute resources (web). Explicit since Aspire 9.4 dropped "hybrid" mode,
// where azd used to silently create/own this environment on the app's behalf — see
// https://learn.microsoft.com/en-us/dotnet/aspire/compatibility/9.4/hybrid-compute-support-dropped.
var containerAppEnvironment = builder.AddAzureContainerAppEnvironment("env");
containerAppEnvironment.ConfigureInfrastructure(infra =>
{
    var env = infra.GetProvisionableResources().OfType<ContainerAppManagedEnvironment>().Single();
    env.Name = BicepFunction.Interpolate($"cae-{Workload}-{EnvToken()}");

    // The environment's own AcrPull identity (distinct from Web's own compute identity, "id-"
    // below) — reachable here since Aspire emits it into this same env.module.bicep. The
    // Container Registry itself ("env-acr") and Web's own identity ("web-identity") are each a
    // *separate* Bicep module/provisioning construct with no IResourceBuilder exposed for either
    // in this file, so — unlike everything else here — they're accepted as out of scope for this
    // pass and still get Aspire's default `envacr<hash>`/`web_identity-<hash>` names; see
    // CLAUDE.md's Deployment section.
    var envIdentity = infra.GetProvisionableResources().OfType<UserAssignedIdentity>().SingleOrDefault();
    if (envIdentity is not null)
    {
        envIdentity.Name = BicepFunction.Interpolate($"id-{Workload}-cae-{EnvToken()}");
    }
});

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
postgres.ConfigureInfrastructure(infra =>
{
    var server = infra.GetProvisionableResources().OfType<PostgreSqlFlexibleServer>().Single();
    // Postgres flexible server names are globally unique (public FQDN) — CAF pattern + hash.
    server.Name = BicepFunction.Interpolate($"pgsql-{Workload}-{EnvToken()}-{ShortHash()}");
});

var dotglassesdb = postgres.AddDatabase("dotglassesdb");

// Provisions the storage account this Bicep needs (Reference Data's ImageUrl field is still a
// plain admin-pasted URL — see CLAUDE.md's [OPEN] items; a real upload feature consuming this
// blob container is separate application-layer work, deliberately not part of this pass).
// RunAsEmulator keeps local `dotnet run` using Azurite (no real Azure Storage account touched in
// dev) while still generating real Azure Storage Bicep at publish time, same pattern as Postgres
// above.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
storage.ConfigureInfrastructure(infra =>
{
    var account = infra.GetProvisionableResources().OfType<StorageAccount>().Single();
    // No hyphens/uppercase allowed, 24-char hard cap — short workload token + a short hash for
    // global uniqueness (storage account names are globally unique across all of Azure).
    account.Name = BicepFunction.Interpolate($"st{ShortWorkload}{EnvToken()}{ShortHash(4)}");
});
var referenceDataImages = storage.AddBlobContainer("reference-data-images");

var web = builder.AddProject<Projects.DotGlasses_Web>("web")
    .WithComputeEnvironment(containerAppEnvironment)
    .WithReference(dotglassesdb)
    .WaitFor(dotglassesdb)
    .WithReference(referenceDataImages)
    .WaitFor(referenceDataImages);
web.PublishAsAzureContainerApp((infra, app) =>
{
    app.Name = BicepFunction.Interpolate($"ca-{Workload}-{EnvToken()}");
});

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

    // Phase 8 (2026-08-12) — the JWT signing key/issuer/audience move out of appsettings into
    // Key Vault for staging/production; appsettings.json's own Jwt section stays deliberately
    // empty outside Development (see JwtOptions' doc comment). Unlike ACS, Key Vault *does* have
    // a real Aspire hosting integration (Aspire.Hosting.Azure.KeyVault) — so this is
    // AddAzureKeyVault + WithReference, not the raw-Bicep-template escape hatch, and RBAC (Key
    // Vault Secrets User on Web's managed identity) is wired automatically by that integration
    // the same way Storage's role assignment already is (see CLAUDE.md's Deployment section).
    // No local emulator exists for Key Vault (same class of gap as ACS), so this whole block is
    // publish-only — plain `dotnet run` keeps reading the literal dev-only key committed in
    // appsettings.Development.json, unaffected.
    var keyVault = builder.AddAzureKeyVault("keyvault");
    keyVault.ConfigureInfrastructure(infra =>
    {
        var vault = infra.GetProvisionableResources().OfType<KeyVaultService>().Single();
        // Key Vault names are globally unique (public DNS) — CAF pattern + hash, same reasoning
        // as Postgres/Storage above.
        vault.Name = BicepFunction.Interpolate($"kv-{Workload}-{EnvToken()}-{ShortHash()}");
    });
    web.WithReference(keyVault);
}

builder.Build().Run();
