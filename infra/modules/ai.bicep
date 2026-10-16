// Microsoft Foundry: parameters and a role assignment only. The Foundry resource, its project and the two model
// deployments are created once, by hand or with `az`, because model choice and quota are account-level decisions.
// This module is deployed into that resource's own resource group and grants the app identities
// Cognitive Services OpenAI User on it, which is all the keyless calls to its /openai/v1/ path need.

@description('Name of the existing Foundry (Azure OpenAI or AI Services) account.')
param accountName string

@description('Principal ids granted Cognitive Services OpenAI User.')
param openAiUserPrincipalIds array

var openAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: accountName
}

resource openAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in openAiUserPrincipalIds: {
    name: guid(account.id, principalId, openAiUserRoleId)
    scope: account
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAiUserRoleId)
    }
  }
]
