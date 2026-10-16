// Container Registry, Basic. The admin user is off and anonymous pull stays at its default, off: the apps pull with
// their managed identities (AcrPull below), and azd or the deploy workflow push with the deployer's Entra ID sign-in.

@description('Azure region.')
param location string

@description('Globally unique registry name, alphanumeric.')
param name string

@description('Tags applied to every resource.')
param tags object

@description('Principal ids of the identities that pull images.')
param pullPrincipalIds array

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in pullPrincipalIds: {
    name: guid(registry.id, principalId, acrPullRoleId)
    scope: registry
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    }
  }
]

output name string = registry.name
output loginServer string = registry.properties.loginServer
