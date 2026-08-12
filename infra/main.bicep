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

@secure()
param postgres_password string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module acs 'acs/acs.bicep' = {
  name: 'acs'
  scope: rg
  params: {
    location: location
  }
}
module env 'env/env.module.bicep' = {
  name: 'env'
  scope: rg
  params: {
    env_acr_outputs_name: env_acr.outputs.name
    location: location
    userPrincipalId: principalId
  }
}
module env_acr 'env-acr/env-acr.module.bicep' = {
  name: 'env-acr'
  scope: rg
  params: {
    location: location
  }
}
module keyvault 'keyvault/keyvault.module.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
  }
}
module postgres 'postgres/postgres.module.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    location: location
  }
}
module storage 'storage/storage.module.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
  }
}
module web_identity 'web-identity/web-identity.module.bicep' = {
  name: 'web-identity'
  scope: rg
  params: {
    location: location
  }
}
module web_roles_keyvault 'web-roles-keyvault/web-roles-keyvault.module.bicep' = {
  name: 'web-roles-keyvault'
  scope: rg
  params: {
    keyvault_outputs_name: keyvault.outputs.name
    location: location
    principalId: web_identity.outputs.principalId
  }
}
module web_roles_postgres 'web-roles-postgres/web-roles-postgres.module.bicep' = {
  name: 'web-roles-postgres'
  scope: rg
  params: {
    location: location
    postgres_outputs_name: postgres.outputs.name
    principalId: web_identity.outputs.principalId
    principalName: web_identity.outputs.principalName
  }
}
module web_roles_storage 'web-roles-storage/web-roles-storage.module.bicep' = {
  name: 'web-roles-storage'
  scope: rg
  params: {
    location: location
    principalId: web_identity.outputs.principalId
    storage_outputs_name: storage.outputs.name
  }
}
output ACS_COMMUNICATIONSERVICECONNECTIONSTRING string = acs.outputs.communicationServiceConnectionString
output ACS_MANAGEDDOMAINNAME string = acs.outputs.managedDomainName
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output ENV_AZURE_CONTAINER_REGISTRY_ENDPOINT string = env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output ENV_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output KEYVAULT_VAULTURI string = keyvault.outputs.vaultUri
output POSTGRES_CONNECTIONSTRING string = postgres.outputs.connectionString
output POSTGRES_HOSTNAME string = postgres.outputs.hostName
output STORAGE_BLOBENDPOINT string = storage.outputs.blobEndpoint
output WEB_IDENTITY_CLIENTID string = web_identity.outputs.clientId
output WEB_IDENTITY_ID string = web_identity.outputs.id
