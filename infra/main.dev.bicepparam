// dev: the team's working environment, rg-pitchside-dev. Free Speech tier, web scales to zero.
// Values that differ per person or per subscription are read from the environment (azd env set, or exported in the
// shell). The defaults are the Foundry resource and deployments recorded in docs/ENVIRONMENT.md: identifiers, not
// secrets. Setting FOUNDRY_ENDPOINT to an empty value keeps the scripted crew.
using 'main.bicep'

param environmentName = 'dev'
param location = readEnvironmentVariable('AZURE_LOCATION', 'southafricanorth')
param owner = readEnvironmentVariable('PITCHSIDE_OWNER', 'maribodevs')

param speechSku = 'F0'
// The existing free-tier account in docs/ENVIRONMENT.md: a subscription may hold only one F0 Speech account.
param speechName = 'spch-pitchside-dev'
// Standard, for the `fabric` consumer group the Fabric Eventstream reads (WP-12, docs/FABRIC.md): Basic holds only
// $Default, which the API's processor holds exclusively. Basic to Standard is an in-place upgrade.
param eventHubsSku = 'Standard'
param webMinReplicas = 0
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
