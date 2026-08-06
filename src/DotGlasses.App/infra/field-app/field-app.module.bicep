@description('Tags to apply to the Static Web App.')
param tags object = {}

// Azure Static Web Apps is only available in a handful of regions (confirmed via Microsoft's own
// azd troubleshooting docs: westus2, centralus, eastus2, westeurope, eastasia) — deliberately
// independent of the shared resource group's own `location` (main.bicep's `location` param,
// used for Postgres/Storage/Container Apps in the root project — not necessarily one of these
// five) rather than reusing it, since that risks an outright deployment failure
// ("LocationNotAvailableForResourceType") the moment the two projects' AZURE_LOCATION doesn't
// happen to be SWA-eligible. Defaults to West Europe (closest of the five to DGI's own base);
// override via `azd env set staticWebAppLocation <region>` if needed.
@allowed(['westus2', 'centralus', 'eastus2', 'westeurope', 'eastasia'])
param location string = 'westeurope'

// Azure CAF naming convention (see the root project's AppHost.cs Workload/EnvToken/ShortHash for
// the identical pattern) — envToken pulls "nonprod"/"prod" straight out of the (shared) resource
// group's own name (rg-dotglasses-nonprod/rg-dotglasses-prod) rather than needing a new parameter
// threaded from main.bicep. Static Web App names are globally unique (public *.azurestaticapps.net
// hostname), hence the uniqueString() suffix, same reasoning as Storage/ACS in the root project.
var envToken = substring(resourceGroup().name, length('rg-dotglasses-'))
var shortHash = take(uniqueString(resourceGroup().id), 6)

// AVM module — matches azd's own quickstart templates (e.g. todo-nodejs-mongo-swa-func) rather
// than a hand-rolled Microsoft.Web/staticSites resource, to avoid guessing at property shapes.
// `provider: 'Custom'` means content is pushed externally via `azd deploy`'s Static Web Apps CLI
// integration (per this project's azure.yaml: host: staticwebapp), not Azure's own
// GitHub-repo-linked build — no repositoryUrl/branch/buildProperties needed or wanted here.
module staticSite 'br/public:avm/res/web/static-site:0.3.0' = {
  name: 'field-app-static-site'
  params: {
    name: 'stapp-dotglasses-app-${envToken}-${shortHash}'
    location: location
    provider: 'Custom'
    sku: 'Free'
    // Must match azure.yaml's service name exactly — this is how azd knows which resource a
    // given service's `azd deploy` targets.
    tags: union(tags, { 'azd-service-name': 'field-app' })
  }
}

output uri string = 'https://${staticSite.outputs.defaultHostname}'
output name string = staticSite.outputs.name
