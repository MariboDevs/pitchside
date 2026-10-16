// One user-assigned managed identity per Container App. Every Azure call the apps make authenticates as one of
// these; the role assignments live next to the resource each one grants access to.

@description('Azure region.')
param location string

@description('dev or demo.')
param environmentName string

@description('Tags applied to every resource.')
param tags object

resource api 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-pitchside-api-${environmentName}'
  location: location
  tags: tags
}

resource agents 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-pitchside-agents-${environmentName}'
  location: location
  tags: tags
}

resource web 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-pitchside-web-${environmentName}'
  location: location
  tags: tags
}

output apiId string = api.id
output apiPrincipalId string = api.properties.principalId
output apiClientId string = api.properties.clientId
output agentsId string = agents.id
output agentsPrincipalId string = agents.properties.principalId
output agentsClientId string = agents.properties.clientId
output webId string = web.id
output webPrincipalId string = web.properties.principalId
output webClientId string = web.properties.clientId
