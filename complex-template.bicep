// Complex Bicep template for testing broader drift detection
param location string = resourceGroup().location
param environmentName string = 'test'
param applicationName string = 'drifttest'
@minValue(5)
@maxValue(13)
param suffixLength int = 8

// Variables with complex expressions
var deploykeyvault = false
var deploystorage = true
var postsuffix = take(uniqueString(resourceGroup().id), suffixLength)
var storageAccountName = '${applicationName}sa${postsuffix}'
var keyVaultName = '${applicationName}kv${postsuffix}'
var vnetName = '${applicationName}-vnet'
var subnetName = '${applicationName}-subnet'

// Virtual Network
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: vnetName
  location: location

  properties: {

    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: '10.0.0.0/24'
        }
      }
      {
        name: '${applicationName}-private-subnet'
        properties: {
          addressPrefix: '10.0.1.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
  tags: {
    Environment: environmentName
    Application: applicationName
  }
  
}

// Storage Account using module
module storageAccountModule 'bicep-modules/storage-account.bicep' = if (deploystorage) {
  name: 'storage-account-deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
    skuName: 'Standard_LRS'
    kind: 'StorageV2'
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    networkAclsDefaultAction: 'Allow'
    virtualNetworkRules: []
    ipRules: []
    tags: {
      Environment: environmentName
      Application: applicationName
      ResourceType: 'Storage'
    }
    isHnsEnabled: false
    largeFileSharesState: 'Disabled'
  }
}

// Key Vault with RBAC
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = if (deploykeyvault) {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForTemplateDeployment: false
    enabledForDiskEncryption: false
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: applicationName
    ResourceType: 'Security'
  }
}

// App Service Plan (conditionally created)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${applicationName}-asp'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: false
    zoneRedundant: false
  }
  tags: {
    Environment: environmentName
    Application: applicationName
  }
}

// Network Security Group - simple test resource
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: '${applicationName}-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowHTTP'
        properties: {
          priority: 100
          access: 'Allow'
          direction: 'Inbound'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowHTTPS'
        properties: {
          priority: 110
          access: 'Allow'
          direction: 'Inbound'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          priority: 1000
          access: 'Deny'
          direction: 'Inbound'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
  tags: {
    Environment: environmentName
    Application: applicationName
  }
}

// Log Analytics Workspace for monitoring
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${applicationName}-law-${postsuffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
  tags: {
    Environment: environmentName
    Application: applicationName
    ResourceType: 'Monitoring'
  }
}

// Outputs
output virtualNetworkId string = virtualNetwork.id
output storageAccountName string = deploystorage ? storageAccountModule.outputs.storageAccountName : ''
output keyVaultName string = keyVault.name
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output networkSecurityGroupId string = networkSecurityGroup.id
//output appServicePlanId string = environmentName != 'prod' ? appServicePlan.id : ''
