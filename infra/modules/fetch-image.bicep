// Reads the image a Container App runs now, so `azd provision` after a deploy keeps the deployed image instead of
// resetting the app to the placeholder. It is a module of its own because a template cannot both reference an
// existing resource and declare it.

@description('True when the app already exists; azd sets SERVICE_<NAME>_RESOURCE_EXISTS before it provisions.')
param exists bool

@description('Container App name.')
param name string

resource app 'Microsoft.App/containerApps@2025-01-01' existing = if (exists) {
  name: name
}

output image string = exists ? app!.properties.template!.containers![0].image! : ''
