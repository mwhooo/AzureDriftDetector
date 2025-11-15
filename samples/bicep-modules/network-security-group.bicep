// Type definitions
@export()
type AccessType = 'Allow' | 'Deny'

@export()
type TrafficDirection = 'Inbound' | 'Outbound'

@export()
type NetworkProtocol = 'Tcp' | 'Udp' | 'Icmp' | '*'

@export()
type SecurityRule = {
  @description('Name of the security rule')
  name: string
  
  @description('Priority of the rule')
  @minValue(100)
  @maxValue(4096)
  priority: int
  
  @description('Access type')
  access: AccessType
  
  @description('Direction of traffic')
  direction: TrafficDirection
  
  @description('Protocol')
  protocol: NetworkProtocol
  
  @description('Source port range')
  sourcePortRange: string
  
  @description('Destination port range')
  destinationPortRange: string
  
  @description('Source address prefix')
  sourceAddressPrefix: string
  
  @description('Destination address prefix')
  destinationAddressPrefix: string
}

@export()
type NsgConfig = {
  @description('Network Security Group name')
  name: string
  
  @description('Location for the NSG')
  location: string?
  
  @description('Security rules configuration')
  securityRules: SecurityRule[]
  
  @description('Tags for the NSG')
  tags: object?
}

// Parameters
@description('Network Security Group configuration')
param nsgConfig NsgConfig

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: nsgConfig.name
  location: nsgConfig.?location ?? resourceGroup().location
  properties: {
    securityRules: [for rule in nsgConfig.securityRules: {
      name: rule.name
      properties: {
        priority: rule.priority
        access: rule.access
        direction: rule.direction
        protocol: rule.protocol
        sourcePortRange: rule.sourcePortRange
        destinationPortRange: rule.destinationPortRange
        sourceAddressPrefix: rule.sourceAddressPrefix
        destinationAddressPrefix: rule.destinationAddressPrefix
      }
    }]
  }
  tags: nsgConfig.?tags ?? {}
}

output nsgId string = networkSecurityGroup.id
output nsgName string = networkSecurityGroup.name
