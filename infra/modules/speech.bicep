// Azure AI Speech for the spoken full-time recap (WP-10). Entra ID authentication needs a custom subdomain, and
// local (key) auth is disabled, so the apps reach it only with Cognitive Services Speech User on their identities.

@description('Azure region.')
param location string

@description('Account name; also the custom subdomain, so globally unique.')
param name string

@description('F0 (free) for dev, S0 for demo.')
@allowed([
  'F0'
  'S0'
])
param sku string

@description('Tags applied to every resource.')
param tags object

@description('Principal ids granted Cognitive Services Speech User.')
param speechUserPrincipalIds array

var speechUserRoleId = 'f2dc8367-1007-4938-bd23-fe263f013447'

resource speech 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'SpeechServices'
  sku: {
    name: sku
  }
  properties: {
    customSubDomainName: name
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource speechUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in speechUserPrincipalIds: {
    name: guid(speech.id, principalId, speechUserRoleId)
    scope: speech
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', speechUserRoleId)
    }
  }
]

output id string = speech.id
output name string = speech.name
output endpoint string = speech.properties.endpoint
