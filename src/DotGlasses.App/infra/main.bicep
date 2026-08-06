targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

var tags = {
  'azd-env-name': environmentName
}

// Deliberately shares the SAME resource group as the root DotGlasses.Web/AppHost azd project
// (rg-${environmentName}) rather than getting its own pair — decided with the user 2026-08-06:
// the Field App and Admin Portal are two facets of one product sharing the same backend, not two
// independent products. This only works because this project's azd environments are named
// identically to the root project's ("dotglasses-nonprod"/"dotglasses-prod" — see CLAUDE.md's
// Deployment section and .github/workflows/deploy.yml); if that ever changes, this becomes a
// third/fourth resource group instead, silently. Resource group creation is idempotent (a plain
// upsert), so it's safe for both azd projects' independent deployments to declare it, in either
// order or in parallel — matching exactly how the root project's own (Aspire-generated)
// main.bicep declares its resource group.
resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module fieldApp 'field-app/field-app.module.bicep' = {
  name: 'field-app'
  scope: rg
  params: {
    tags: tags
  }
}

output FIELD_APP_URI string = fieldApp.outputs.uri
output FIELD_APP_NAME string = fieldApp.outputs.name
