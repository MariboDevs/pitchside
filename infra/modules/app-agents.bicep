// pitchside-agents: the Microsoft Agent Framework worker host (Pitchside.Agents.Host). It is a worker with no HTTP
// surface, so it has no ingress at all, which is stricter than internal ingress, and no HTTP probes. It scales from
// 0 to 2 replicas on the unprocessed events in the match-events hub, through the KEDA azure-eventhub scaler
// authenticated with the app's managed identity (no connection string).

@description('Azure region.')
param location string

@description('Tags applied to every resource, without the azd service tag.')
param tags object

@description('Container Apps environment id.')
param environmentId string

@description('Resource id of the agents identity.')
param identityId string

@description('Client id of the agents identity, given to DefaultAzureCredential as AZURE_CLIENT_ID.')
param identityClientId string

@description('Registry login server.')
param registryServer string

@description('Image to run; empty runs the placeholder until the first azd deploy.')
param image string

@description('Image that stands in before the first deploy.')
param placeholderImage string

@description('Event Hubs namespace name (not the FQDN), as the KEDA scaler expects it.')
param eventHubsNamespaceName string

@description('Event Hubs fully qualified namespace.')
param eventHubsNamespace string

@description('Event hub name.')
param eventHubName string

@description('Consumer group whose checkpoints the scaler measures lag against.')
param consumerGroup string

@description('Checkpoint storage account name.')
param checkpointStorageAccountName string

@description('Checkpoint blob container name.')
param checkpointContainerName string

@description('Blob container URI for processor checkpoints.')
param checkpointBlobUri string

@description('Foundry endpoint; empty keeps the scripted crew.')
param foundryEndpoint string

@description('Foundry deployment for the explainer, persona writer and translator.')
param foundryFastDeployment string

@description('Foundry deployment for half-time and full-time narratives and the agent fact-check.')
param foundryQualityDeployment string

var appName = 'pitchside-agents'
var useFoundry = !empty(foundryEndpoint)

var baseSettings = [
  { name: 'AZURE_CLIENT_ID', value: identityClientId }
  { name: 'OTEL_SERVICE_NAME', value: appName }
  { name: 'Bus__Provider', value: 'EventHubs' }
  { name: 'Bus__EventHubs__Namespace', value: eventHubsNamespace }
  { name: 'Bus__EventHubs__HubName', value: eventHubName }
  { name: 'Bus__EventHubs__CheckpointBlobUri', value: checkpointBlobUri }
  { name: 'Agents__Provider', value: useFoundry ? 'Foundry' : 'Scripted' }
]

var foundrySettings = useFoundry
  ? [
      { name: 'Foundry__Endpoint', value: foundryEndpoint }
      { name: 'Foundry__Deployments__Fast', value: foundryFastDeployment }
      { name: 'Foundry__Deployments__Quality', value: foundryQualityDeployment }
    ]
  : []

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'agents' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: registryServer
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'agents'
          image: empty(image) ? placeholderImage : image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(baseSettings, foundrySettings)
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
        rules: [
          {
            name: 'match-events-lag'
            custom: {
              type: 'azure-eventhub'
              identity: identityId
              metadata: {
                eventHubNamespace: eventHubsNamespaceName
                eventHubName: eventHubName
                consumerGroup: consumerGroup
                checkpointStrategy: 'blobMetadata'
                storageAccountName: checkpointStorageAccountName
                blobContainer: checkpointContainerName
                unprocessedEventThreshold: '64'
                activationUnprocessedEventThreshold: '1'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = app.name
