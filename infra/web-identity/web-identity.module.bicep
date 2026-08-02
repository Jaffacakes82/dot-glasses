@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource web_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('web_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = web_identity.id

output clientId string = web_identity.properties.clientId

output principalId string = web_identity.properties.principalId

output principalName string = web_identity.name

output name string = web_identity.name