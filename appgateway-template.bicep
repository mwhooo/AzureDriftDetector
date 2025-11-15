@description('Application Gateway stress test template')

// Parameters
param location string = resourceGroup().location
param applicationName string
param vnetConfig object
param appGatewayConfig object
param tags object = {}

// Virtual Network (simplified for AppGW testing)
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: vnetConfig.name
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: vnetConfig.addressSpaces
    }
    subnets: [for subnet in vnetConfig.subnets: {
      name: subnet.name
      properties: {
        addressPrefix: subnet.addressPrefix
        privateEndpointNetworkPolicies: subnet.privateEndpointNetworkPolicies
        privateLinkServiceNetworkPolicies: subnet.privateLinkServiceNetworkPolicies
      }
    }]
    enableDdosProtection: vnetConfig.enableDdosProtection
  }
}

// Get the Application Gateway subnet
var appGwSubnet = virtualNetwork.properties.subnets[0]

// Application Gateway module
module applicationGateway 'bicep-modules/application-gateway.bicep' = {
  name: '${applicationName}-appgw-module'
  params: {
    appGatewayConfig: appGatewayConfig
    location: location
    tags: tags
    subnetId: '${virtualNetwork.id}/subnets/${appGwSubnet.name}'
  }
}

// Outputs
output virtualNetworkId string = virtualNetwork.id
output applicationGatewayId string = applicationGateway.outputs.applicationGatewayId
output publicIpAddress string = applicationGateway.outputs.publicIpAddress
output fqdn string = applicationGateway.outputs.fqdn
