// Type definitions
@export()
type EnableState = 'Disabled' | 'Enabled'

@export()
type Subnet = {
  @description('Name of the subnet')
  name: string
  
  @description('Address prefix for the subnet')
  addressPrefix: string
  
  @description('Private endpoint network policies')
  privateEndpointNetworkPolicies: EnableState?
  
  @description('Private link service network policies')
  privateLinkServiceNetworkPolicies: EnableState?
  
  @description('Network security group resource ID')
  networkSecurityGroupId: string?
  
  @description('Route table resource ID')
  routeTableId: string?
}

@export()
type VnetConfig = {
  @description('Virtual network name')
  name: string
  
  @description('Location for the virtual network')
  location: string?
  
  @description('Address space for the virtual network')
  addressSpaces: string[]?
  
  @description('Subnets configuration')
  subnets: Subnet[]
  
  @description('Enable DDoS protection')
  enableDdosProtection: bool?
  
  @description('DDoS protection plan resource ID (required if enableDdosProtection is true)')
  ddosProtectionPlanId: string?
  
  @description('Tags for the virtual network')
  tags: object?
  
  @description('DNS servers for the virtual network')
  dnsServers: string[]?
}

// Parameters
@description('Virtual network configuration')
param vnetConfig VnetConfig

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: vnetConfig.name
  location: vnetConfig.?location ?? resourceGroup().location
  tags: vnetConfig.?tags ?? {}
  properties: {
    addressSpace: {
      addressPrefixes: vnetConfig.?addressSpaces ?? ['10.0.0.0/16']
    }
    subnets: [for subnet in vnetConfig.subnets: {
      name: subnet.name
      properties: {
        addressPrefix: subnet.addressPrefix
        privateEndpointNetworkPolicies: subnet.?privateEndpointNetworkPolicies ?? 'Enabled'
        privateLinkServiceNetworkPolicies: subnet.?privateLinkServiceNetworkPolicies ?? 'Enabled'
        networkSecurityGroup: !empty(subnet.?networkSecurityGroupId ?? '') ? {
          id: subnet.?networkSecurityGroupId
        } : null
        routeTable: !empty(subnet.?routeTableId ?? '') ? {
          id: subnet.?routeTableId
        } : null
      }
    }]
    dhcpOptions: !empty(vnetConfig.?dnsServers ?? []) ? {
      dnsServers: vnetConfig.?dnsServers
    } : null
    enableDdosProtection: vnetConfig.?enableDdosProtection ?? false
    ddosProtectionPlan: (vnetConfig.?enableDdosProtection ?? false) && !empty(vnetConfig.?ddosProtectionPlanId ?? '') ? {
      id: vnetConfig.?ddosProtectionPlanId
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
output subnets array = [for (subnet, i) in vnetConfig.subnets: {
  name: virtualNetwork.properties.subnets[i].name
  id: virtualNetwork.properties.subnets[i].id
  addressPrefix: virtualNetwork.properties.subnets[i].properties.addressPrefix
}]
