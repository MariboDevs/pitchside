// pitchside-api: match actors, stats, SignalR hub (/hubs/match), REST (/api/v1) and the MCP server (/mcp).
// Internal ingress, so pitchside-web's nginx is the one way in and the usage guard can trust the address it forwards,
// with sticky sessions, exactly one replica because the Akka.NET actor system is a single node, and
// probes on the ServiceDefaults endpoints /alive (liveness) and /health (readiness). WebSockets need no switch on
// Container Apps: the HTTP ingress upgrades them by default.

@description('Azure region.')
param location string

@description('Tags applied to every resource, without the azd service tag.')
param tags object

@description('Container Apps environment id.')
param environmentId string

@description('Resource id of the API identity.')
param identityId string

@description('Client id of the API identity, given to DefaultAzureCredential as AZURE_CLIENT_ID.')
param identityClientId string

@description('Registry login server.')
param registryServer string

@description('Image to run; empty runs the placeholder until the first azd deploy.')
param image string

@description('Image that stands in before the first deploy; it listens on 8080 like the real one.')
param placeholderImage string

@description('Cosmos DB account endpoint.')
param cosmosEndpoint string

@description('Cosmos DB database name.')
param cosmosDatabase string

@description('Event Hubs fully qualified namespace.')
param eventHubsNamespace string

@description('Event hub name.')
param eventHubName string

@description('Blob container URI for processor checkpoints.')
param checkpointBlobUri string

@description('Foundry endpoint; empty keeps the scripted crew.')
param foundryEndpoint string

@description('Foundry deployment for the explainer, persona writer and translator.')
param foundryFastDeployment string

@description('Foundry deployment for half-time and full-time narratives and the agent fact-check.')
param foundryQualityDeployment string

@description('Whether the API hosts the agent crew in process.')
param runAgentsInApi bool

@description('Whether the API starts the demo seed on its own and loops it.')
param demoAutoStart bool

@description('The demo replay\'s speed over real time; 1 is real time.')
param demoSpeed string

@description('The highest demo loop number started.')
param demoMaxLoops int

@description('Key Vault URI.')
param keyVaultUri string

@description('Read Mcp:ApiKey from the Key Vault secret mcp-api-key; the secret must exist before this is turned on.')
param mcpApiKeyFromKeyVault bool

@description('The Speech resource id the spoken recap (WP-10) names in its Entra ID token.')
param speechResourceId string

@description('The blob service the spoken recaps are kept in.')
param recapBlobServiceUri string

var appName = 'pitchside-api'
var port = 8080
var usePlaceholder = empty(image)
var useFoundry = !empty(foundryEndpoint)

var baseSettings = [
  { name: 'AZURE_CLIENT_ID', value: identityClientId }
  { name: 'OTEL_SERVICE_NAME', value: appName }
  { name: 'Storage__Provider', value: 'Cosmos' }
  { name: 'Storage__Cosmos__AccountEndpoint', value: cosmosEndpoint }
  { name: 'Storage__Cosmos__Database', value: cosmosDatabase }
  { name: 'Bus__Provider', value: 'EventHubs' }
  { name: 'Bus__EventHubs__Namespace', value: eventHubsNamespace }
  { name: 'Bus__EventHubs__HubName', value: eventHubName }
  { name: 'Bus__EventHubs__CheckpointBlobUri', value: checkpointBlobUri }
  { name: 'Pitchside__RunAgentsInApi', value: string(runAgentsInApi) }
  // WP-11: the demo loop. Kept here so a provision cannot drop it; the overlay scales its card timings by the speed.
  { name: 'Demo__AutoStart', value: string(demoAutoStart) }
  { name: 'Demo__Speed', value: demoSpeed }
  { name: 'Demo__MaxLoops', value: string(demoMaxLoops) }
  { name: 'Agents__Provider', value: useFoundry ? 'Foundry' : 'Scripted' }
  // The app is public and calls paid services (Cosmos DB, Event Hubs) even with the scripted crew, so the usage
  // limits always apply here rather than following the crew's provider.
  { name: 'UsageGuard__Enabled', value: 'true' }
  // Two hops append to X-Forwarded-For before the API: nginx (the viewer's address) and this app's ingress (nginx's).
  { name: 'UsageGuard__ForwardedHops', value: '2' }
  // WP-10: the full-time recap is spoken by Azure AI Speech, keyless, and kept in Blob Storage.
  { name: 'Speech__Provider', value: 'Azure' }
  { name: 'Speech__Region', value: location }
  { name: 'Speech__ResourceId', value: speechResourceId }
  { name: 'Speech__BlobServiceUri', value: recapBlobServiceUri }
]

var foundrySettings = useFoundry
  ? [
      { name: 'Foundry__Endpoint', value: foundryEndpoint }
      { name: 'Foundry__Deployments__Fast', value: foundryFastDeployment }
      { name: 'Foundry__Deployments__Quality', value: foundryQualityDeployment }
      { name: 'Evaluation__Provider', value: 'Foundry' }
    ]
  : []

var mcpSettings = mcpApiKeyFromKeyVault ? [{ name: 'Mcp__ApiKey', secretRef: 'mcp-api-key' }] : []

var secrets = mcpApiKeyFromKeyVault
  ? [
      {
        name: 'mcp-api-key'
        keyVaultUrl: '${keyVaultUri}secrets/mcp-api-key'
        identity: identityId
      }
    ]
  : []

var probes = usePlaceholder
  ? []
  : [
      {
        type: 'Startup'
        httpGet: { path: '/alive', port: port }
        periodSeconds: 5
        failureThreshold: 24
      }
      {
        type: 'Liveness'
        httpGet: { path: '/alive', port: port }
        periodSeconds: 15
        failureThreshold: 3
      }
      {
        type: 'Readiness'
        httpGet: { path: '/health', port: port }
        periodSeconds: 10
        failureThreshold: 3
      }
    ]

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
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
      ingress: {
        external: false
        targetPort: port
        transport: 'auto'
        allowInsecure: false
        stickySessions: {
          affinity: 'sticky'
        }
      }
      registries: [
        {
          server: registryServer
          identity: identityId
        }
      ]
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'api'
          image: usePlaceholder ? placeholderImage : image
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          env: concat(baseSettings, foundrySettings, mcpSettings)
          probes: probes
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output name string = app.name
output fqdn string = app.properties.configuration!.ingress!.fqdn!
output uri string = 'https://${app.properties.configuration!.ingress!.fqdn!}'
