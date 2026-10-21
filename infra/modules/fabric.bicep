// Optional F2 Microsoft Fabric capacity (WP-12), for when the trial capacity in docs/ENVIRONMENT.md ends or a
// workspace needs its own. Azure creates a capacity running, and a running capacity bills by the hour: pause it
// straight after the first deployment, and whenever nobody is demonstrating, with infra/scripts/fabric-capacity.sh.
// Enabling Fabric for the tenant and assigning a workspace to the capacity are Fabric admin portal steps.

@description('Azure region; Fabric must be offered there.')
param location string

@description('Capacity name: lowercase letters and digits, 3 to 63 characters.')
param name string

@description('Tags applied to every resource.')
param tags object

@description('Capacity administrators: user principal names or service principal object ids. At least one.')
@minLength(1)
param administrators array

resource capacity 'Microsoft.Fabric/capacities@2023-11-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'F2'
    tier: 'Fabric'
  }
  properties: {
    administration: {
      members: administrators
    }
  }
}

output id string = capacity.id
output name string = capacity.name
