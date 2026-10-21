// Container Registry, Basic. The admin user is off and anonymous pull stays at its default, off: the apps pull with
// their managed identities (AcrPull below), and azd or the deploy workflow push with the deployer's Entra ID sign-in
// (AcrPush below for the deploy workflow's identity; a person running azd needs Owner or Contributor already).

@description('Azure region.')
param location string

@description('Globally unique registry name, alphanumeric.')
param name string

@description('Tags applied to every resource.')
param tags object

@description('Principal ids of the identities that pull images.')
param pullPrincipalIds array

@description('Principal id of the deploy workflow\'s identity, which pushes images. Empty skips the role.')
param pushPrincipalId string = ''

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'

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

resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(pushPrincipalId)) {
  name: guid(registry.id, pushPrincipalId, acrPushRoleId)
  scope: registry
  properties: {
    principalId: pushPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleId)
  }
}

output name string = registry.name
output loginServer string = registry.properties.loginServer
