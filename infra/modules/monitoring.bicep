// Log Analytics workspace, workspace-based Application Insights and the alert action group. The workspace sits here
// rather than in environment.bicep because Application Insights is built on it and the Container Apps environment's
// OpenTelemetry agent needs Application Insights at creation; keeping all three together avoids a cycle.
// The alert rules themselves need the API's address, so they are in alerts.bicep, deployed after the apps.

@description('Azure region.')
param location string

@description('dev or demo.')
param environmentName string

@description('Tags applied to every resource.')
param tags object

@description('Addresses the alerts email. Empty means no action group and alerts that only show in the portal.')
param alertEmails array

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-pitchside-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

// Ingestion stays on the connection string because the Container Apps OpenTelemetry agent sends with it and cannot
// use Entra ID; the string is read from the resource at deployment time and never written to a file.
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-pitchside-${environmentName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    RetentionInDays: 30
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmails)) {
  name: 'ag-pitchside-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'pitchside'
    enabled: true
    emailReceivers: [
      for (address, index) in alertEmails: {
        name: 'email-${index}'
        emailAddress: address
        useCommonAlertSchema: true
      }
    ]
  }
}

output workspaceId string = workspace.id
output workspaceName string = workspace.name
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
output actionGroupId string = empty(alertEmails) ? '' : actionGroup.id
