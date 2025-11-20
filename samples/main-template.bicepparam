using 'main-template.bicep'

// Common parameters
param location = 'westeurope'
param environmentName = 'test'
param applicationName = 'drifttest'

// Common Tags
param tags = {
  Environment: 'test'
  Application: 'drifttest'
  ResourceType: 'Infrastructure'
}

// Storage Account Configuration
param storageConfig = {
  storageAccountName: 'drifttestsa'
  skuName: 'Standard_LRS'
  kind: 'StorageV2'
  accessTier: 'Hot'
  allowBlobPublicAccess: false
  allowSharedKeyAccess: true
  minimumTlsVersion: 'TLS1_2'
  supportsHttpsTrafficOnly: true
  networkAclsDefaultAction: 'Allow'
  isHnsEnabled: false
  largeFileSharesState: 'Disabled'
}

// Virtual Network Configuration
param vnetConfig = {
  name: 'drifttest-vnet'
  addressSpaces: ['10.0.0.0/16']
  subnets: [
    {
      name: 'drifttest-subnet'
      addressPrefix: '10.0.0.0/24'
      privateEndpointNetworkPolicies: 'Disabled'
      privateLinkServiceNetworkPolicies: 'Enabled'
    }
    {
      name: 'drifttest-private-subnet'
      addressPrefix: '10.0.1.0/24'
      privateEndpointNetworkPolicies: 'Disabled'
      privateLinkServiceNetworkPolicies: 'Enabled'
    }
     {
      name: 'drifttest-private-subnet-2'
      addressPrefix: '10.0.2.0/24'
      privateEndpointNetworkPolicies: 'Disabled'
      privateLinkServiceNetworkPolicies: 'Enabled'
    }
  ]
  enableDdosProtection: false
}

// Network Security Group Configuration
param nsgConfig = {
  name: 'drifttest-nsg'
  securityRules: [
    {
      name: 'AllowHTTP'
      priority: 100
      access: 'Allow'
      direction: 'Inbound'
      protocol: 'Tcp'
      sourcePortRange: '*'
      destinationPortRange: '80'
      sourceAddressPrefix: '*'
      destinationAddressPrefix: '*'
    }
    {
      name: 'AllowHTTPS'
      priority: 110
      access: 'Allow'
      direction: 'Inbound'
      protocol: 'Tcp'
      sourcePortRange: '*'
      destinationPortRange: '443'
      sourceAddressPrefix: '*'
      destinationAddressPrefix: '*'
    }
    {
      name: 'DenyAllInbound'
      priority: 1000
      access: 'Deny'
      direction: 'Inbound'
      protocol: '*'
      sourcePortRange: '*'
      destinationPortRange: '*'
      sourceAddressPrefix: '*'
      destinationAddressPrefix: '*'
    }
  ]
}

// App Service Plan Configuration
param appServicePlanConfig = {
  name: 'drifttest-asp'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  reserved: false
  zoneRedundant: false
}

// Log Analytics Workspace Configuration
param logAnalyticsConfig = {
  name: 'drifttest-law'
  skuName: 'PerGB2018'
  retentionInDays: 30
  enableLogAccessUsingOnlyResourcePermissions: true
}

// Key Vault Configuration (optional)
param keyVaultConfig = {
  name: 'drifttest-kv'
  sku: {
    family: 'A'
    name: 'standard'
  }
  enabledForDeployment: false
  enabledForTemplateDeployment: false
  enabledForDiskEncryption: false
  enableRbacAuthorization: true
  publicNetworkAccess: 'Enabled'
}

// Service Bus Configuration (Basic tier - FREE)
param serviceBusConfig = {
  name: 'drifttest-servicebus'
  skuName: 'Basic'
  disableLocalAuth: false
  publicNetworkAccess: 'Enabled'
  minimumTlsVersion: '1.2'
  queues: [
    {
      name: 'orders'
      maxDeliveryCount: 10
      lockDuration: 'PT5M'
      requiresDuplicateDetection: false
      requiresSession: false
      deadLetteringOnMessageExpiration: false
    }
    {
      name: 'deadletter'
      maxDeliveryCount: 1
      lockDuration: 'PT1M'
      requiresDuplicateDetection: false
      requiresSession: false
      deadLetteringOnMessageExpiration: false
    }
    {
      name: 'notifications'
      maxDeliveryCount: 5
      lockDuration: 'PT2M'
      requiresDuplicateDetection: false
      requiresSession: false
      deadLetteringOnMessageExpiration: false
    }
  ]
  topics: []
}

// Deployment Flags
param deployVnet = true
param deployNsg = true
param deployStorage = true
param deployAppServicePlan = true
param deployLogAnalytics = true
param deployKeyVault = false
param deployServiceBus = false
