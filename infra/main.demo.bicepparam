// demo: the judged environment, rg-pitchside-demo, frozen from 25 October to 10 November. Standard Speech tier and
// one warm web replica so judges never wait on a cold start (the API is always exactly one replica).
// Values that differ per person or per subscription are read from the environment (azd env set, or exported in the
// shell). The defaults are the Foundry resource and deployments recorded in docs/ENVIRONMENT.md: identifiers, not
// secrets. Setting FOUNDRY_ENDPOINT to an empty value keeps the scripted crew.
using 'main.bicep'

param environmentName = 'demo'
param location = readEnvironmentVariable('AZURE_LOCATION', 'southafricanorth')
param owner = readEnvironmentVariable('PITCHSIDE_OWNER', 'maribodevs')

param speechSku = 'S0'
// Basic: demo has no `fabric` consumer group, so no Eventstream reads it (WP-12, docs/FABRIC.md).
param eventHubsSku = 'Basic'
param webMinReplicas = 1
param runAgentsInApi = true

param foundryEndpoint = readEnvironmentVariable('FOUNDRY_ENDPOINT', 'https://ais-pitchside-dev.openai.azure.com/')
param foundryFastDeployment = readEnvironmentVariable('FOUNDRY_DEPLOYMENT_FAST', 'pitchside-fast')
param foundryQualityDeployment = readEnvironmentVariable('FOUNDRY_DEPLOYMENT_QUALITY', 'pitchside-quality')
param foundryResourceId = readEnvironmentVariable('FOUNDRY_RESOURCE_ID', '/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourceGroups/rg-pitchside-dev/providers/Microsoft.CognitiveServices/accounts/ais-pitchside-dev')

// The deploy workflow's identity, id-pitchside-github in docs/ENVIRONMENT.md: AcrPush so it can push images.
param deployerPrincipalId = readEnvironmentVariable('PITCHSIDE_DEPLOYER_PRINCIPAL_ID', '06983e89-3126-499b-ac88-193588a256e9')

// Comma-separated, set with `azd env set PITCHSIDE_ALERT_EMAILS ...`; empty means no action group.
param alertEmails = empty(readEnvironmentVariable('PITCHSIDE_ALERT_EMAILS', ''))
  ? []
  : split(readEnvironmentVariable('PITCHSIDE_ALERT_EMAILS', ''), ',')

param mcpApiKeyFromKeyVault = bool(readEnvironmentVariable('PITCHSIDE_MCP_KEY_FROM_VAULT', 'false'))

// WP-12. The Fabric workspace identity's object id, once workspace identity is on (docs/FABRIC.md); empty grants
// nothing. The F2 capacity is off: the workspace runs on the trial capacity in docs/ENVIRONMENT.md.
param fabricWorkspacePrincipalId = readEnvironmentVariable('FABRIC_WORKSPACE_PRINCIPAL_ID', '')
param fabricCapacity = bool(readEnvironmentVariable('PITCHSIDE_FABRIC_CAPACITY', 'false'))
param fabricCapacityAdministrators = empty(readEnvironmentVariable('PITCHSIDE_FABRIC_ADMINS', ''))
  ? []
  : split(readEnvironmentVariable('PITCHSIDE_FABRIC_ADMINS', ''), ',')

// azd sets these before it provisions, so a provision after a deploy keeps the deployed images.
param apiExists = bool(readEnvironmentVariable('SERVICE_API_RESOURCE_EXISTS', 'false'))
param agentsExists = bool(readEnvironmentVariable('SERVICE_AGENTS_RESOURCE_EXISTS', 'false'))
param webExists = bool(readEnvironmentVariable('SERVICE_WEB_RESOURCE_EXISTS', 'false'))
