@description('Virtual network name')
param vnetName string

@description('Location for the virtual network')
param location string = resourceGroup().location

@description('Address space for the virtual network')
param addressSpaces array = ['10.0.0.0/16']

@description('Subnets configuration')
param subnets array

@description('Enable DDoS protection')
param enableDdosProtection bool = false

@description('DDoS protection plan resource ID (required if enableDdosProtection is true)')
param ddosProtectionPlanId string = ''

@description('Tags for the virtual network')
param tags object = {}

@description('DNS servers for the virtual network')
param dnsServers array = []

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: addressSpaces
    }
    subnets: [for subnet in subnets: {
      name: subnet.name
      properties: {
        addressPrefix: subnet.addressPrefix
        privateEndpointNetworkPolicies: subnet.?privateEndpointNetworkPolicies ?? 'Enabled'
        privateLinkServiceNetworkPolicies: subnet.?privateLinkServiceNetworkPolicies ?? 'Enabled'
        networkSecurityGroup: !empty(subnet.?networkSecurityGroupId ?? '') ? {
          id: subnet.networkSecurityGroupId
        } : null
        routeTable: !empty(subnet.?routeTableId ?? '') ? {
          id: subnet.routeTableId
        } : null
      }
    }]
    dhcpOptions: !empty(dnsServers) ? {
      dnsServers: dnsServers
    } : null
    enableDdosProtection: enableDdosProtection
    ddosProtectionPlan: enableDdosProtection && !empty(ddosProtectionPlanId) ? {
      id: ddosProtectionPlanId
    } : null
  }
}

@description('Virtual network resource ID')
output vnetId string = virtualNetwork.id

@description('Virtual network name')
output vnetName string = virtualNetwork.name

@description('Virtual network address space')
output addressSpaces array = virtualNetwork.properties.addressSpace.addressPrefixes

@description('Subnet details')
output subnets array = [for (subnet, i) in subnets: {
  name: virtualNetwork.properties.subnets[i].name
  id: virtualNetwork.properties.subnets[i].id
  addressPrefix: virtualNetwork.properties.subnets[i].properties.addressPrefix
}]
