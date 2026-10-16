// demo: the judged environment, rg-pitchside-demo, frozen from 25 October to 10 November. Standard Speech tier and
// one warm web replica so judges never wait on a cold start (the API is always exactly one replica).
// Values that differ per person or per subscription are read from the environment (azd env set, or exported in the
// shell), so nothing here is a secret and nothing real is committed: an empty Foundry endpoint keeps the scripted crew.
using 'main.bicep'

param environmentName = 'demo'
param location = readEnvironmentVariable('AZURE_LOCATION', 'southafricanorth')
param owner = readEnvironmentVariable('PITCHSIDE_OWNER', 'maribodevs')

param speechSku = 'S0'
param eventHubsSku = 'Basic'
param webMinReplicas = 1
param runAgentsInApi = true

param foundryEndpoint = readEnvironmentVariable('FOUNDRY_ENDPOINT', '')
param foundryFastDeployment = readEnvironmentVariable('FOUNDRY_DEPLOYMENT_FAST', '')
param foundryQualityDeployment = readEnvironmentVariable('FOUNDRY_DEPLOYMENT_QUALITY', '')
param foundryResourceId = readEnvironmentVariable('FOUNDRY_RESOURCE_ID', '')

// Comma-separated, set with `azd env set PITCHSIDE_ALERT_EMAILS ...`; empty means no action group.
param alertEmails = empty(readEnvironmentVariable('PITCHSIDE_ALERT_EMAILS', ''))
  ? []
  : split(readEnvironmentVariable('PITCHSIDE_ALERT_EMAILS', ''), ',')

param mcpApiKeyFromKeyVault = bool(readEnvironmentVariable('PITCHSIDE_MCP_KEY_FROM_VAULT', 'false'))

// azd sets these before it provisions, so a provision after a deploy keeps the deployed images.
param apiExists = bool(readEnvironmentVariable('SERVICE_API_RESOURCE_EXISTS', 'false'))
param agentsExists = bool(readEnvironmentVariable('SERVICE_AGENTS_RESOURCE_EXISTS', 'false'))
param webExists = bool(readEnvironmentVariable('SERVICE_WEB_RESOURCE_EXISTS', 'false'))
