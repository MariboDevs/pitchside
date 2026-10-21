// Pitchside in Azure: one resource group per environment (rg-pitchside-dev, rg-pitchside-demo) holding the Container
// Apps, their identities and every backing service. Everything is identity-based: no key, connection string or
// password is a parameter, an output or an app setting. Values per environment are in main.dev.bicepparam and
// main.demo.bicepparam; infra/README.md explains how azd picks one.

targetScope = 'subscription'

@description('Environment name; also the azd environment name.')
@allowed([
  'dev'
  'demo'
])
param environmentName string

@description('Azure region for every resource. Record the choice in docs/specs/WP-09-infrastructure.md.')
param location string

@description('Value of the owner tag.')
param owner string

@description('Speech SKU: F0 for dev, S0 for demo.')
@allowed([
  'F0'
  'S0'
])
param speechSku string

@description('Name of the Speech account; empty generates one. dev reuses the existing free-tier account, since a subscription may hold only one F0 Speech account.')
param speechName string = ''

@description('Event Hubs SKU. Basic holds only the $Default consumer group; Standard adds api, agents and fabric, which the Fabric Eventstream needs.')
@allowed([
  'Basic'
  'Standard'
])
param eventHubsSku string = 'Basic'

@description('Minimum replicas of pitchside-web: 0 lets dev scale to zero, 1 keeps the demo warm.')
@minValue(0)
@maxValue(2)
param webMinReplicas int = 0

@description('Whether the API hosts the agent crew in process (Pitchside:RunAgentsInApi).')
param runAgentsInApi bool = true

@description('Foundry resource endpoint, such as https://<name>.openai.azure.com/. Empty keeps the scripted crew.')
param foundryEndpoint string = ''

@description('Foundry deployment for the explainer, persona writer and translator.')
param foundryFastDeployment string = ''

@description('Foundry deployment for half-time and full-time narratives and the agent fact-check.')
param foundryQualityDeployment string = ''

@description('Resource id of the Foundry account, for the Cognitive Services OpenAI User role. Empty skips it.')
param foundryResourceId string = ''

@description('Principal id of the deploy workflow\'s identity, for AcrPush on the registry. Empty skips it.')
param deployerPrincipalId string = ''

@description('Addresses the alerts email. Empty creates no action group; nothing real is committed.')
param alertEmails array = []

@description('Read Mcp:ApiKey from the Key Vault secret mcp-api-key. Create the secret by hand first.')
param mcpApiKeyFromKeyVault bool = false

@description('Object id of the Fabric workspace identity that reads the hub (WP-12, docs/FABRIC.md). Empty grants nothing.')
param fabricWorkspacePrincipalId string = ''

@description('Create the optional F2 Fabric capacity (WP-12). It starts running: pause it with infra/scripts/fabric-capacity.sh.')
param fabricCapacity bool = false

@description('Administrators of the F2 capacity, required when fabricCapacity is true.')
param fabricCapacityAdministrators array = []

@description('Set by azd: whether pitchside-api already exists, so its deployed image is kept.')
param apiExists bool = false

@description('Set by azd: whether pitchside-agents already exists, so its deployed image is kept.')
param agentsExists bool = false

@description('Set by azd: whether pitchside-web already exists, so its deployed image is kept.')
param webExists bool = false

var tags = {
  project: 'pitchside'
  env: environmentName
  owner: owner
}

// azd finds the resource group by this tag.
var resourceGroupTags = union(tags, { 'azd-env-name': environmentName })

var token = uniqueString(subscription().id, environmentName, location)

// Listens on 8080 like the real images, so the target port never changes between the first provision and the first
// deploy. Probes are attached only once a real image runs.
var placeholderImage = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

var eventHubName = 'match-events'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-pitchside-${environmentName}'
  location: location
  tags: resourceGroupTags
}

module identity 'modules/identity.bicep' = {
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    tags: tags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    alertEmails: alertEmails
  }
}

module environment 'modules/environment.bicep' = {
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    workspaceId: monitoring.outputs.workspaceId
    appInsightsName: monitoring.outputs.appInsightsName
  }
}

module registry 'modules/registry.bicep' = {
  scope: rg
  params: {
    location: location
    name: 'crpitchside${environmentName}${token}'
    tags: tags
    pullPrincipalIds: [
      identity.outputs.apiPrincipalId
      identity.outputs.agentsPrincipalId
      identity.outputs.webPrincipalId
    ]
    pushPrincipalId: deployerPrincipalId
  }
}

module cosmos 'modules/cosmos.bicep' = {
  scope: rg
  params: {
    location: location
    name: 'cosmos-pitchside-${environmentName}-${token}'
    tags: tags
    dataContributorPrincipalIds: [
      identity.outputs.apiPrincipalId
      identity.outputs.agentsPrincipalId
    ]
  }
}

module eventHubs 'modules/eventhubs.bicep' = {
  scope: rg
  params: {
    location: location
    namespaceName: 'evhns-pitchside-${environmentName}-${token}'
    storageAccountName: take('stpitch${environmentName}${token}', 24)
    tags: tags
    sku: eventHubsSku
    hubName: eventHubName
    principalIds: [
      identity.outputs.apiPrincipalId
      identity.outputs.agentsPrincipalId
    ]
    fabricReaderPrincipalId: fabricWorkspacePrincipalId
  }
}

module fabric 'modules/fabric.bicep' = if (fabricCapacity) {
  scope: rg
  params: {
    location: location
    name: 'fcpitchside${environmentName}'
    tags: tags
    administrators: fabricCapacityAdministrators
  }
}

module keyVault 'modules/keyvault.bicep' = {
  scope: rg
  params: {
    location: location
    name: take('kv-pitch-${environmentName}-${token}', 24)
    tags: tags
    secretReaderPrincipalIds: [
      identity.outputs.apiPrincipalId
    ]
  }
}

module speech 'modules/speech.bicep' = {
  scope: rg
  params: {
    location: location
    name: empty(speechName) ? 'spch-pitchside-${environmentName}-${token}' : speechName
    sku: speechSku
    tags: tags
    speechUserPrincipalIds: [
      identity.outputs.apiPrincipalId
      identity.outputs.agentsPrincipalId
    ]
  }
}

// The Foundry account lives outside this template, possibly in another resource group; only the role is granted.
module ai 'modules/ai.bicep' = if (!empty(foundryResourceId)) {
  name: 'ai-role-${environmentName}'
  scope: resourceGroup(split(foundryResourceId, '/')[2], split(foundryResourceId, '/')[4])
  params: {
    accountName: last(split(foundryResourceId, '/'))
    openAiUserPrincipalIds: [
      identity.outputs.apiPrincipalId
      identity.outputs.agentsPrincipalId
    ]
  }
}

module apiImage 'modules/fetch-image.bicep' = {
  scope: rg
  params: {
    exists: apiExists
    name: 'pitchside-api'
  }
}

module agentsImage 'modules/fetch-image.bicep' = {
  scope: rg
  params: {
    exists: agentsExists
    name: 'pitchside-agents'
  }
}

module webImage 'modules/fetch-image.bicep' = {
  scope: rg
  params: {
    exists: webExists
    name: 'pitchside-web'
  }
}

module api 'modules/app-api.bicep' = {
  scope: rg
  params: {
    location: location
    tags: tags
    environmentId: environment.outputs.id
    identityId: identity.outputs.apiId
    identityClientId: identity.outputs.apiClientId
    registryServer: registry.outputs.loginServer
    image: apiImage.outputs.image == placeholderImage ? '' : apiImage.outputs.image
    placeholderImage: placeholderImage
    cosmosEndpoint: cosmos.outputs.endpoint
    cosmosDatabase: cosmos.outputs.databaseName
    eventHubsNamespace: eventHubs.outputs.fullyQualifiedNamespace
    eventHubName: eventHubs.outputs.hubName
    checkpointBlobUri: eventHubs.outputs.checkpointBlobUri
    speechResourceId: speech.outputs.id
    recapBlobServiceUri: eventHubs.outputs.blobServiceUri
    foundryEndpoint: foundryEndpoint
    foundryFastDeployment: foundryFastDeployment
    foundryQualityDeployment: foundryQualityDeployment
    runAgentsInApi: runAgentsInApi
    keyVaultUri: keyVault.outputs.uri
    mcpApiKeyFromKeyVault: mcpApiKeyFromKeyVault
  }
}

module agents 'modules/app-agents.bicep' = {
  scope: rg
  // The Cosmos data-plane role assignment must exist before the host first runs; nothing else orders it.
  // The Speech module, with its role assignment, is ordered by speechResourceId below.
  dependsOn: [
    cosmos
  ]
  params: {
    location: location
    tags: tags
    environmentId: environment.outputs.id
    identityId: identity.outputs.agentsId
    identityClientId: identity.outputs.agentsClientId
    registryServer: registry.outputs.loginServer
    image: agentsImage.outputs.image == placeholderImage ? '' : agentsImage.outputs.image
    placeholderImage: placeholderImage
    eventHubsNamespaceName: eventHubs.outputs.namespaceName
    eventHubsNamespace: eventHubs.outputs.fullyQualifiedNamespace
    eventHubName: eventHubs.outputs.hubName
    // The scaler measures lag in this group from its checkpoints. While the crew runs in the API, nothing reads the
    // agents group, so its lag would never drain and the idle host would stay scaled out: it follows $Default, which
    // the API checkpoints. The agents group is used only on Standard with the crew in its own host.
    consumerGroup: eventHubsSku == 'Basic' || runAgentsInApi ? '$Default' : 'agents'
    checkpointStorageAccountName: eventHubs.outputs.storageAccountName
    checkpointContainerName: eventHubs.outputs.checkpointContainerName
    checkpointBlobUri: eventHubs.outputs.checkpointBlobUri
    speechResourceId: speech.outputs.id
    recapBlobServiceUri: eventHubs.outputs.blobServiceUri
    foundryEndpoint: foundryEndpoint
    foundryFastDeployment: foundryFastDeployment
    foundryQualityDeployment: foundryQualityDeployment
  }
}

module web 'modules/app-web.bicep' = {
  scope: rg
  params: {
    location: location
    tags: tags
    environmentId: environment.outputs.id
    identityId: identity.outputs.webId
    registryServer: registry.outputs.loginServer
    image: webImage.outputs.image == placeholderImage ? '' : webImage.outputs.image
    placeholderImage: placeholderImage
    apiFqdn: api.outputs.fqdn
    minReplicas: webMinReplicas
  }
}

module alerts 'modules/alerts.bicep' = {
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    appInsightsId: monitoring.outputs.appInsightsId
    apiHealthUrl: '${api.outputs.uri}/health'
    actionGroupId: monitoring.outputs.actionGroupId
  }
}

// azd reads these after provisioning; none of them is a secret.
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.name
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = environment.outputs.name
output API_URL string = api.outputs.uri
output WEB_URL string = web.outputs.uri
output COSMOS_ENDPOINT string = cosmos.outputs.endpoint
output EVENT_HUBS_NAMESPACE string = eventHubs.outputs.fullyQualifiedNamespace
output EVENT_HUBS_HUB string = eventHubs.outputs.hubName
output EVENT_HUBS_FABRIC_CONSUMER_GROUP string = eventHubs.outputs.fabricConsumerGroup
output FABRIC_CAPACITY_NAME string = fabricCapacity ? fabric!.outputs.name : ''
output KEY_VAULT_NAME string = keyVault.outputs.name
output SPEECH_ENDPOINT string = speech.outputs.endpoint
output SPEECH_RESOURCE_ID string = speech.outputs.id
output API_IDENTITY_CLIENT_ID string = identity.outputs.apiClientId
output AGENTS_IDENTITY_CLIENT_ID string = identity.outputs.agentsClientId
