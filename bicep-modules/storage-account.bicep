@description('Name of the storage account')
param storageAccountName string

@description('Location for the storage account')
param location string = resourceGroup().location

@description('Storage account SKU')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
  'Standard_ZRS'
  'Premium_LRS'
  'Premium_ZRS'
])
param skuName string = 'Standard_LRS'

@description('Storage account kind')
@allowed([
  'Storage'
  'StorageV2'
  'BlobStorage'
  'FileStorage'
  'BlockBlobStorage'
])
param kind string = 'StorageV2'

@description('Storage account access tier')
@allowed([
  'Hot'
  'Cool'
])
param accessTier string = 'Hot'

@description('Allow public access to blobs')
param allowBlobPublicAccess bool = false

@description('Allow shared key access')
param allowSharedKeyAccess bool = true

@description('Minimum TLS version')
@allowed([
  'TLS1_0'
  'TLS1_1'
  'TLS1_2'
])
param minimumTlsVersion string = 'TLS1_2'

@description('Enable HTTPS traffic only')
param supportsHttpsTrafficOnly bool = true

@description('Network access configuration')
@allowed([
  'Allow'
  'Deny'
])
param networkAclsDefaultAction string = 'Allow'

@description('Virtual network rules')
param virtualNetworkRules array = []

@description('IP rules for firewall')
param ipRules array = []

@description('Tags for the storage account')
param tags object = {}

@description('Enable hierarchical namespace (Data Lake Gen2)')
param isHnsEnabled bool = false

@description('Large file shares state')
@allowed([
  'Disabled'
  'Enabled'
])
param largeFileSharesState string = 'Disabled'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: kind
  sku: {
    name: skuName
  }
  tags: tags
  properties: {
    accessTier: accessTier
    allowBlobPublicAccess: allowBlobPublicAccess
    allowSharedKeyAccess: allowSharedKeyAccess
    minimumTlsVersion: minimumTlsVersion
    supportsHttpsTrafficOnly: supportsHttpsTrafficOnly
    isHnsEnabled: isHnsEnabled
    largeFileSharesState: largeFileSharesState
    networkAcls: {
      defaultAction: networkAclsDefaultAction
      virtualNetworkRules: virtualNetworkRules
      ipRules: ipRules
      bypass: 'AzureServices'
    }
  }
}

@description('Storage account resource ID')
output storageAccountId string = storageAccount.id

@description('Storage account name')
output storageAccountName string = storageAccount.name

@description('Primary endpoints')
output primaryEndpoints object = storageAccount.properties.primaryEndpoints

@description('Storage account primary blob endpoint')
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('Storage account primary file endpoint')
output primaryFileEndpoint string = storageAccount.properties.primaryEndpoints.file
