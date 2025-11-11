// Sample Bicep template for testing drift detection
param location string = resourceGroup().location
param storageAccountName string = 'testdriftsa${uniqueString(resourceGroup().id)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
  tags: {}
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'testdriftkv${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enableRbacAuthorization: true
  }
  tags: {
    Environment: 'Test'
    Purpose: 'DriftDetection'
  }
}

output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
