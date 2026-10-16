// Cosmos DB for NoSQL, serverless, with continuous backup (7 days) and local (key) auth disabled. The database and
// its containers are created here because Entra ID data-plane roles cannot create them: CosmosMatchStore only checks
// they exist when it runs against Azure. Container names match CosmosMatchStore exactly, all partitioned on /matchId.

@description('Azure region.')
param location string

@description('Globally unique account name, lowercase.')
param name string

@description('Tags applied to every resource.')
param tags object

@description('Database name; Storage:Cosmos:Database in the apps.')
param databaseName string = 'pitchside'

@description('Principal ids granted the built-in Cosmos DB Data Contributor data-plane role.')
param dataContributorPrincipalIds array

// CosmosMatchStore.Matches, Events, FactSheets, Moments, Narratives, Overlay and Audit.
var containerNames = [
  'matches'
  'events'
  'factsheets'
  'moments'
  'narratives'
  'overlay'
  'audit'
]

// Cosmos DB Built-in Data Contributor, a data-plane SQL role, not an Azure RBAC role.
var dataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: name
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
    disableLocalAuth: true
    disableKeyBasedMetadataWriteAccess: true
    minimalTlsVersion: 'Tls12'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource containers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = [
  for containerName in containerNames: {
    parent: database
    name: containerName
    properties: {
      resource: {
        id: containerName
        partitionKey: {
          paths: [
            '/matchId'
          ]
          kind: 'Hash'
        }
      }
    }
  }
]

resource dataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = [
  for principalId in dataContributorPrincipalIds: {
    parent: account
    name: guid(account.id, principalId, dataContributorRoleId)
    properties: {
      principalId: principalId
      roleDefinitionId: '${account.id}/sqlRoleDefinitions/${dataContributorRoleId}'
      scope: account.id
    }
  }
]

output name string = account.name
output endpoint string = account.properties.documentEndpoint
output databaseName string = database.name
