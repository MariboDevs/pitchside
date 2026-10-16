// Key Vault in RBAC mode. It holds only what cannot be identity-based: today nothing, and the MCP API key
// (secret name mcp-api-key) if the team sets one by hand. Nothing in this template writes a secret value.

@description('Azure region.')
param location string

@description('Vault name, globally unique, at most 24 characters.')
param name string

@description('Tags applied to every resource.')
param tags object

@description('Principal ids that read secrets (Key Vault Secrets User).')
param secretReaderPrincipalIds array

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    // Seven days, the minimum, and purge protection left off, so `azd down --purge` can remove the vault after
    // judging instead of holding its name for 90 days.
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in secretReaderPrincipalIds: {
    name: guid(vault.id, principalId, keyVaultSecretsUserRoleId)
    scope: vault
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    }
  }
]

output name string = vault.name
output uri string = vault.properties.vaultUri
