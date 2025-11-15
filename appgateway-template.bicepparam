using 'appgateway-template.bicep'

// Common parameters
param location = 'westeurope'
param applicationName = 'drifttest'

// Virtual Network Configuration (simplified for AppGW)
param vnetConfig = {
  name: 'drifttest-vnet'
  addressSpaces: ['10.0.0.0/16']
  subnets: [
    {
      name: 'drifttest-appgw-subnet'
      addressPrefix: '10.0.2.0/24'
      privateEndpointNetworkPolicies: 'Disabled'
      privateLinkServiceNetworkPolicies: 'Enabled'
    }
  ]
  enableDdosProtection: false
}

// Application Gateway Configuration
param appGatewayConfig = {
  name: 'drifttest-appgw'
  sku: {
    name: 'Standard_v2'
    tier: 'Standard_v2'
  }
  capacity: {
    min: 1
    max: 3
  }
  frontendPorts: [
    {
      name: 'port-80'
      port: 80
    }
    {
      name: 'port-443'
      port: 443
    }
  ]
  backendPools: [
    {
      name: 'backend-pool-web'
      addresses: ['example.com', 'backup.example.com']
    }
  ]
  httpListeners: [
    {
      name: 'listener-http'
      frontendPortName: 'port-80'
      protocol: 'Http'
      hostName: 'myapp.example.com'
    }
  ]
  routingRules: [
    {
      name: 'rule-basic'
      listenerName: 'listener-http'
      backendPoolName: 'backend-pool-web'
      priority: 100
    }
  ]
  enableHttp2: true
}

// Common Tags
param tags = {
  Environment: 'test'
  Application: 'drifttest'
  ResourceType: 'ApplicationGateway'
}
