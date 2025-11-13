// Clean main template using UDTs and modules

// Import config types from modules
import {StorageAccountConfig} from 'bicep-modules/storage-account.bicep'
import {VnetConfig} from 'bicep-modules/virtual-network.bicep'
import {NsgConfig} from 'bicep-modules/network-security-group.bicep'
import {AppServicePlanConfig} from 'bicep-modules/app-service-plan.bicep'
import {LogAnalyticsConfig} from 'bicep-modules/log-analytics-workspace.bicep'
import {KeyVaultConfig} from 'bicep-modules/key-vault.bicep'

@description('Common parameters')
param location string = resourceGroup().location
param environmentName string = 'test'
param applicationName string = 'drifttest'

@description('Storage account configuration')
param storageConfig StorageAccountConfig

@description('Virtual network configuration')  
param vnetConfig VnetConfig

@description('Network security group configuration')
param nsgConfig NsgConfig

@description('App Service Plan configuration')
param appServicePlanConfig AppServicePlanConfig

@description('Log Analytics Workspace configuration')
param logAnalyticsConfig LogAnalyticsConfig?

@description('Key Vault configuration')
param keyVaultConfig KeyVaultConfig?

@description('Common resource tags')
param tags object

@description('Deployment switches')
param deployStorage bool = true
param deployKeyVault bool = false

// Generate unique suffix for resource names
var uniqueSuffix = take(uniqueString(resourceGroup().id), 8)

// Merge common parameters into config objects
var vnetConfigWithCommon = union(vnetConfig, {location: location, tags: tags})
var nsgConfigWithCommon = union(nsgConfig, {location: location, tags: tags})
var storageConfigWithCommon = union(storageConfig, {location: location, tags: tags, storageAccountName: '${storageConfig.storageAccountName}${uniqueSuffix}'})
var appServicePlanConfigWithCommon = union(appServicePlanConfig, {location: location, tags: tags})

// Virtual Network Module
module vnetModule 'bicep-modules/virtual-network.bicep' = {
  name: 'vnet-deployment'
  params: {
    vnetConfig: vnetConfigWithCommon
  }
}

// Network Security Group Module
module nsgModule 'bicep-modules/network-security-group.bicep' = {
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
module appServicePlanModule 'bicep-modules/app-service-plan.bicep' = {
  name: 'app-service-plan-deployment'
  params: {
    appServicePlanConfig: appServicePlanConfigWithCommon
  }
}

// Log Analytics Workspace Module
// module logAnalyticsModule 'bicep-modules/log-analytics-workspace.bicep' = {
//   name: 'log-analytics-deployment'
//   params: {
//     workspaceName: '${logAnalyticsConfig.name}-${uniqueSuffix}'
//     location: location
//     skuName: logAnalyticsConfig.sku
//     retentionInDays: logAnalyticsConfig.retentionInDays
//     enableLogAccessUsingOnlyResourcePermissions: logAnalyticsConfig.enableLogAccessUsingOnlyResourcePermissions
//     tags: tags
//   }
// }

// // Key Vault Module (conditional)
// module keyVaultModule 'bicep-modules/key-vault.bicep' = if (deployKeyVault && keyVaultConfig != null) {
//   name: 'key-vault-deployment'
//   params: {
//     keyVaultName: '${keyVaultConfig!.name}${uniqueSuffix}'
//     location: location
//     sku: {
//       family: keyVaultConfig!.skuFamily
//       name: keyVaultConfig!.skuName
//     }
//     enabledForDeployment: keyVaultConfig!.enabledForDeployment
//     enabledForTemplateDeployment: keyVaultConfig!.enabledForTemplateDeployment
//     enabledForDiskEncryption: keyVaultConfig!.enabledForDiskEncryption
//     enableRbacAuthorization: keyVaultConfig!.enableRbacAuthorization
//     publicNetworkAccess: keyVaultConfig!.publicNetworkAccess
//     tags: tags
//   }
// }

// Outputs
output vnetId string = vnetModule.outputs.vnetId
output storageAccountName string = deployStorage ? storageModule!.outputs.storageAccountName : ''
output appServicePlanId string = appServicePlanModule.outputs.appServicePlanId
//output logAnalyticsWorkspaceId string = logAnalyticsModule.outputs.workspaceId
//output keyVaultName string = deployKeyVault && keyVaultConfig != null ? keyVaultModule!.outputs.keyVaultName : ''
output nsgId string = nsgModule.outputs.nsgId
output environmentName string = environmentName
output applicationName string = applicationName
