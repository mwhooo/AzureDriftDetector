// Clean main template using modules with simplified parameter approach

@description('Common parameters')
param location string = resourceGroup().location
param environmentName string
param applicationName string

@description('Storage account configuration')
param storageConfig object

@description('Virtual network configuration')  
param vnetConfig object

@description('Network security group configuration')
param nsgConfig object

@description('App Service Plan configuration')
param appServicePlanConfig object

@description('Log Analytics Workspace configuration')
param logAnalyticsConfig object?

@description('Key Vault configuration')
param keyVaultConfig object?

@description('Service Bus configuration')
param serviceBusConfig object?

@description('Common resource tags')
param tags object

@description('Deployment switches')
param deployVnet bool
param deployNsg bool
param deployStorage bool
param deployAppServicePlan bool
param deployLogAnalytics bool
param deployKeyVault bool
param deployServiceBus bool

// Generate unique suffix for resource names
var uniqueSuffix = take(uniqueString(resourceGroup().id), 8)

// Merge common parameters into config objects
var vnetConfigWithCommon = union(vnetConfig, {location: location, tags: tags})
var nsgConfigWithCommon = union(nsgConfig, {location: location, tags: tags})
var storageConfigWithCommon = union(storageConfig, {location: location, tags: tags, storageAccountName: '${storageConfig.storageAccountName}${uniqueSuffix}'})
var appServicePlanConfigWithCommon = union(appServicePlanConfig, {location: location, tags: tags})
var logAnalyticsConfigWithCommon = logAnalyticsConfig != null ? union(logAnalyticsConfig!, {location: location, tags: tags, name: '${logAnalyticsConfig!.name}-${uniqueSuffix}'}) : null
var keyVaultConfigWithCommon = keyVaultConfig != null ? union(keyVaultConfig!, {location: location, tags: tags, name: '${keyVaultConfig!.name}${uniqueSuffix}'}) : null
var serviceBusConfigWithCommon = serviceBusConfig != null ? union(serviceBusConfig!, {location: location, tags: tags, name: serviceBusConfig!.name}) : null

// Virtual Network Module
module vnetModule 'bicep-modules/virtual-network.bicep' = if (deployVnet) {
  name: 'vnet-deployment'
  params: {
    vnetConfig: vnetConfigWithCommon
  }
}

// Network Security Group Module
module nsgModule 'bicep-modules/network-security-group.bicep' = if (deployNsg) {
  name: 'nsg-deployment'
  params: {
    nsgConfig: nsgConfigWithCommon
  }
}

// Storage Account Module
module storageModule 'bicep-modules/storage-account.bicep' = if (deployStorage) {
  name: 'storage-deployment'
  params: {
    storageAccountConfig: storageConfigWithCommon
  }
}

// App Service Plan Module
module appServicePlanModule 'bicep-modules/app-service-plan.bicep' = if (deployAppServicePlan) {
  name: 'app-service-plan-deployment'
  params: {
    appServicePlanConfig: appServicePlanConfigWithCommon
  }
}

// Log Analytics Workspace Module
module logAnalyticsModule 'bicep-modules/log-analytics-workspace.bicep' = if (deployLogAnalytics && logAnalyticsConfig != null) {
  name: 'log-analytics-deployment'
  params: {
    logAnalyticsConfig: logAnalyticsConfigWithCommon!
  }
}

// Key Vault Module (conditional)
module keyVaultModule 'bicep-modules/key-vault.bicep' = if (deployKeyVault && keyVaultConfig != null) {
  name: 'key-vault-deployment'
  params: {
    keyVaultConfig: keyVaultConfigWithCommon!
  }
}

// Service Bus Module (conditional)
module serviceBusModule 'bicep-modules/service-bus.bicep' = if (deployServiceBus && serviceBusConfig != null) {
  name: 'service-bus-deployment'
  params: {
    serviceBusConfig: serviceBusConfigWithCommon!
  }
}

// Outputs
output vnetId string = deployVnet ? vnetModule!.outputs.vnetId : ''
output storageAccountName string = deployStorage ? storageModule!.outputs.storageAccountName : ''
output appServicePlanId string = deployAppServicePlan ? appServicePlanModule!.outputs.appServicePlanId : ''
output logAnalyticsWorkspaceId string = deployLogAnalytics && logAnalyticsConfig != null ? logAnalyticsModule!.outputs.workspaceId : ''
output keyVaultName string = deployKeyVault && keyVaultConfig != null ? keyVaultModule!.outputs.keyVaultName : ''
output serviceBusNamespaceName string = deployServiceBus && serviceBusConfig != null ? serviceBusModule!.outputs.serviceBusNamespaceName : ''
output nsgId string = deployNsg ? nsgModule!.outputs.nsgId : ''
output environmentName string = environmentName
output applicationName string = applicationName
