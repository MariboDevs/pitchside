// Event Hubs namespace and the match-events hub, plus the Storage account the EventProcessorClient checkpoints into.
// Both refuse keys: the namespace has local auth disabled and the storage account refuses shared-key access, so the
// apps reach them only through their managed identities.

@description('Azure region.')
param location string

@description('Namespace name, globally unique.')
param namespaceName string

@description('Checkpoint storage account name, globally unique, lowercase alphanumeric, at most 24 characters.')
param storageAccountName string

@description('Tags applied to every resource.')
param tags object

@description('Basic allows only the $Default consumer group; Standard allows the named ones.')
@allowed([
  'Basic'
  'Standard'
])
param sku string

@description('Principal ids that publish to and read from the hub, and checkpoint into the storage account.')
param principalIds array

@description('Hub name; Bus:EventHubs:HubName in the apps.')
param hubName string = 'match-events'

@description('Blob container for processor checkpoints.')
param checkpointContainerName string = 'checkpoints'

// The API's ingest reads $Default (BusOptions.IngestConsumerGroup maps to it). The named groups exist for the
// separate agent host and the Fabric eventstream; Basic cannot hold them, so they are created on Standard only.
var namedConsumerGroups = [
  'api'
  'agents'
  'fabric'
]

var eventHubsDataSenderRoleId = '2b629674-e913-4c01-ae53-ef4638d8f975'
var eventHubsDataReceiverRoleId = 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource namespace 'Microsoft.EventHub/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
    capacity: 1
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    isAutoInflateEnabled: false
  }
}

resource hub 'Microsoft.EventHub/namespaces/eventhubs@2024-01-01' = {
  parent: namespace
  name: hubName
  properties: {
    partitionCount: 4
    messageRetentionInDays: 1
  }
}

resource consumerGroups 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2024-01-01' = [
  for group in (sku == 'Basic' ? [] : namedConsumerGroups): {
    parent: hub
    name: group
  }
]

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource checkpoints 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: checkpointContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource sender 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(namespace.id, principalId, eventHubsDataSenderRoleId)
    scope: namespace
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', eventHubsDataSenderRoleId)
    }
  }
]

resource receiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(namespace.id, principalId, eventHubsDataReceiverRoleId)
    scope: namespace
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', eventHubsDataReceiverRoleId)
    }
  }
]

resource blobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    name: guid(storage.id, principalId, storageBlobDataContributorRoleId)
    scope: storage
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    }
  }
]

output namespaceName string = namespace.name
output fullyQualifiedNamespace string = '${namespace.name}.servicebus.windows.net'
output hubName string = hub.name
output storageAccountName string = storage.name
output checkpointContainerName string = checkpoints.name
output checkpointBlobUri string = '${storage.properties.primaryEndpoints.blob}${checkpoints.name}'
// The spoken recaps (WP-10) are kept in the same account, in their own container, which the apps create.
output blobServiceUri string = storage.properties.primaryEndpoints.blob
