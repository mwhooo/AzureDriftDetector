// Type definitions
@export()
type ApplicationGatewayConfig = {
  name: string
  sku: {
    name: 'Standard_v2' | 'WAF_v2'
    tier: 'Standard_v2' | 'WAF_v2'
  }
  capacity: {
    min: int
    max: int
  }
  frontendPorts: {
    name: string
    port: int
  }[]
  backendPools: {
    name: string
    addresses: string[]
  }[]
  httpListeners: {
    name: string
    frontendPortName: string
    protocol: 'Http' | 'Https'
    hostName: string?
  }[]
  routingRules: {
    name: string
    listenerName: string
    backendPoolName: string
    priority: int
  }[]
  enableHttp2: bool?
  firewallMode: 'Detection' | 'Prevention'?
}

// Parameters
@description('Configuration for Application Gateway')
param appGatewayConfig ApplicationGatewayConfig

@description('Location for the Application Gateway')
param location string = resourceGroup().location

@description('Resource tags')
param tags object = {}

@description('Subnet ID for Application Gateway')
param subnetId string

resource publicIp 'Microsoft.Network/publicIPAddresses@2023-05-01' = {
  name: '${appGatewayConfig.name}-pip'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    dnsSettings: {
      domainNameLabel: toLower('${appGatewayConfig.name}-${uniqueString(resourceGroup().id)}')
    }
  }
}

resource applicationGateway 'Microsoft.Network/applicationGateways@2023-05-01' = {
  name: appGatewayConfig.name
  location: location
  tags: tags
  properties: {
    sku: {
      name: appGatewayConfig.sku.name
      tier: appGatewayConfig.sku.tier
    }
    autoscaleConfiguration: {
      minCapacity: appGatewayConfig.capacity.min
      maxCapacity: appGatewayConfig.capacity.max
    }
    enableHttp2: appGatewayConfig.?enableHttp2 ?? true
    gatewayIPConfigurations: [
      {
        name: 'appGatewayIpConfig'
        properties: {
          subnet: {
            id: subnetId
          }
        }
      }
    ]
    frontendIPConfigurations: [
      {
        name: 'appGatewayFrontendIP'
        properties: {
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
    frontendPorts: [for port in appGatewayConfig.frontendPorts: {
      name: port.name
      properties: {
        port: port.port
      }
    }]
    backendAddressPools: [for pool in appGatewayConfig.backendPools: {
      name: pool.name
      properties: {
        backendAddresses: map(pool.addresses, addr => {
          fqdn: addr
        })
      }
    }]
    backendHttpSettingsCollection: [
      {
        name: 'appGatewayBackendHttpSettings'
        properties: {
          port: 80
          protocol: 'Http'
          cookieBasedAffinity: 'Disabled'
          pickHostNameFromBackendAddress: false
          requestTimeout: 20
        }
      }
    ]
    httpListeners: [for listener in appGatewayConfig.httpListeners: {
      name: listener.name
      properties: {
        frontendIPConfiguration: {
          id: resourceId('Microsoft.Network/applicationGateways/frontendIPConfigurations', appGatewayConfig.name, 'appGatewayFrontendIP')
        }
        frontendPort: {
          id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', appGatewayConfig.name, listener.frontendPortName)
        }
        protocol: listener.protocol
        hostName: listener.?hostName
      }
    }]
    requestRoutingRules: [for rule in appGatewayConfig.routingRules: {
      name: rule.name
      properties: {
        priority: rule.priority
        ruleType: 'Basic'
        httpListener: {
          id: resourceId('Microsoft.Network/applicationGateways/httpListeners', appGatewayConfig.name, rule.listenerName)
        }
        backendAddressPool: {
          id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', appGatewayConfig.name, rule.backendPoolName)
        }
        backendHttpSettings: {
          id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', appGatewayConfig.name, 'appGatewayBackendHttpSettings')
        }
      }
    }]
    webApplicationFirewallConfiguration: appGatewayConfig.sku.tier == 'WAF_v2' ? {
      enabled: true
      firewallMode: appGatewayConfig.?firewallMode ?? 'Detection'
      ruleSetType: 'OWASP'
      ruleSetVersion: '3.2'
    } : null
  }
}

output applicationGatewayId string = applicationGateway.id
output publicIpAddress string = publicIp.properties.ipAddress
output fqdn string = publicIp.properties.dnsSettings.fqdn
