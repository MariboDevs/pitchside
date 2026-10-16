// The two alerts from the spec, deployed after the apps because the first one needs the API's address.
// 1. API unavailable for 5 minutes: a standard availability test on /health from five locations, alerting when two
//    or more fail within five minutes.
// 2. Narratives fallback rate above 20% in 15 minutes: a log query over the moment workflow's completion log line
//    ("Moment {MomentId}: base narrative {Status} after {Attempts} explainer attempts", MomentWorkflow.LogCompleted),
//    which reaches Application Insights through the environment's OpenTelemetry agent.

@description('Azure region.')
param location string

@description('dev or demo.')
param environmentName string

@description('Tags applied to every resource.')
param tags object

@description('Application Insights component id.')
param appInsightsId string

@description('API health URL the availability test calls.')
param apiHealthUrl string

@description('Action group id; empty means the alerts fire without notifying anyone.')
param actionGroupId string

var actions = empty(actionGroupId) ? [] : [{ actionGroupId: actionGroupId }]
var availabilityTestName = 'webtest-pitchside-api-health-${environmentName}'

resource availability 'Microsoft.Insights/webtests@2022-06-15' = {
  name: availabilityTestName
  location: location
  // The hidden-link tag ties the test to its Application Insights component; the portal and the alert need it.
  tags: union(tags, { 'hidden-link:${appInsightsId}': 'Resource' })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: availabilityTestName
    Name: 'API /health (${environmentName})'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      { Id: 'emea-nl-ams-azr' }
      { Id: 'emea-gb-db3-azr' }
      { Id: 'emea-se-sto-edge' }
      { Id: 'emea-fr-pra-edge' }
      { Id: 'us-va-ash-azr' }
    ]
    Request: {
      RequestUrl: apiHealthUrl
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource apiUnavailable 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-pitchside-api-unavailable-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    description: 'The API /health endpoint failed from two or more locations within five minutes.'
    severity: 1
    enabled: true
    scopes: [
      availability.id
      appInsightsId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: availability.id
      componentId: appInsightsId
      failedLocationCount: 2
    }
    actions: actions
  }
}

resource fallbackRate 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'alert-pitchside-fallback-rate-${environmentName}'
  location: location
  tags: tags
  properties: {
    displayName: 'Narratives fallback rate above 20% (${environmentName})'
    description: 'More than 20% of base narratives in the last 15 minutes fell back to the code template.'
    severity: 2
    enabled: true
    scopes: [
      appInsightsId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: '''
traces
| where message has "base narrative" and message has "explainer attempts"
| parse message with * "base narrative " status:string " after " *
| summarize total = count(), fallback = countif(status =~ "Fallback")
| where total > 0 and todouble(fallback) / total > 0.2
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: empty(actionGroupId) ? [] : [actionGroupId]
    }
  }
}
