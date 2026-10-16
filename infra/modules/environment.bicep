// Container Apps environment on the Consumption workload profile. Console and system logs go to Log Analytics
// through a diagnostic setting (no workspace key), and the managed OpenTelemetry agent forwards the apps' OTLP traces
// and logs to Application Insights: ServiceDefaults exports OTLP whenever OTEL_EXPORTER_OTLP_ENDPOINT is set, which
// the agent sets on every app.

@description('Azure region.')
param location string

@description('dev or demo.')
param environmentName string

@description('Tags applied to every resource.')
param tags object

@description('Resource id of the Log Analytics workspace.')
param workspaceId string

@description('Name of the Application Insights component in this resource group.')
param appInsightsName string

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// A preview API version: appInsightsConfiguration and openTelemetryConfiguration (the managed OpenTelemetry agent)
// are not in a GA version of the type definitions this Bicep release ships.
resource environment 'Microsoft.App/managedEnvironments@2025-02-02-preview' = {
  name: 'cae-pitchside-${environmentName}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    appInsightsConfiguration: {
      connectionString: appInsights.properties.ConnectionString
    }
    openTelemetryConfiguration: {
      tracesConfiguration: {
        destinations: [
          'appInsights'
        ]
      }
      logsConfiguration: {
        destinations: [
          'appInsights'
        ]
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    zoneRedundant: false
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: environment
  properties: {
    workspaceId: workspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
  }
}

output id string = environment.id
output name string = environment.name
output defaultDomain string = environment.properties.defaultDomain
