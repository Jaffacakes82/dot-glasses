// Hand-authored — no Aspire hosting integration exists for Azure Communication Services (the
// only two published Aspire.Hosting packages that touch it are for SMS/Chat samples, not a real
// resource-provisioning integration; confirmed via `dotnet package search` before writing this).
// Referenced from AppHost.cs via AddBicepTemplate so it still participates in the regenerable
// `azd infra gen` pipeline instead of becoming hand-maintained loose Bicep under /infra directly.
//
// Provisions the base Communication Services resource + an Email Communication Service with a
// free Azure-managed domain (instant, no DNS verification needed — a custom verified domain is a
// real follow-up but is an interactive, portal-driven step that can't be scripted here). Wires
// the Email service to the Communication Service via a domain association, and outputs the
// Communication Service's connection string for IEmailSender to consume once a real
// implementation replaces LoggingEmailSender (see CLAUDE.md's [OPEN] items — that swap is
// deliberately out of scope for this pass; only the infra needs to exist).
@description('The location used for the Email Communication Service (data location, not the resource location — ACS itself is always global).')
param location string = resourceGroup().location

@description('Data residency for the Email Communication Service.')
param dataLocation string = 'United States'

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'acs-email-${uniqueString(resourceGroup().id)}'
  location: 'global'
  properties: {
    dataLocation: dataLocation
  }
}

resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${uniqueString(resourceGroup().id)}'
  location: 'global'
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      managedDomain.id
    ]
  }
}

output communicationServiceConnectionString string = communicationService.listKeys().primaryConnectionString
output communicationServiceEndpoint string = 'https://${communicationService.properties.hostName}'
output managedDomainName string = managedDomain.properties.mailFromSenderDomain
