// pitchside-web: nginx serving the Angular build and reverse-proxying /api, /hubs (WebSocket upgrade) and /mcp to the
// API, so the browser talks to one origin and the API needs no CORS. External ingress, 0 to 2 replicas (demo keeps one
// warm). API_HOST is the API's FQDN; nginx calls it over HTTPS inside the environment.

@description('Azure region.')
param location string

@description('Tags applied to every resource, without the azd service tag.')
param tags object

@description('Container Apps environment id.')
param environmentId string

@description('Resource id of the web identity.')
param identityId string

@description('Registry login server.')
param registryServer string

@description('Image to run; empty runs the placeholder until the first azd deploy.')
param image string

@description('Image that stands in before the first deploy; it listens on 8080 like the real one.')
param placeholderImage string

@description('FQDN of the API app, which nginx proxies to.')
param apiFqdn string

@description('Minimum replicas: 0 lets dev scale to zero, 1 keeps the demo warm.')
@minValue(0)
@maxValue(2)
param minReplicas int

var appName = 'pitchside-web'
var port = 8080
var usePlaceholder = empty(image)

var probes = usePlaceholder
  ? []
  : [
      {
        type: 'Liveness'
        httpGet: { path: '/healthz', port: port }
        periodSeconds: 15
        failureThreshold: 3
      }
      {
        type: 'Readiness'
        httpGet: { path: '/healthz', port: port }
        periodSeconds: 10
        failureThreshold: 3
      }
    ]

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
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
        external: true
        targetPort: port
        transport: 'auto'
        allowInsecure: false
      }
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
          name: 'web'
          image: usePlaceholder ? placeholderImage : image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'API_HOST', value: apiFqdn }
          ]
          probes: probes
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: 2
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = app.name
output fqdn string = app.properties.configuration!.ingress!.fqdn!
output uri string = 'https://${app.properties.configuration!.ingress!.fqdn!}'
