// Type definitions
@export()
type StorageAccountSku = 'Standard_LRS' | 'Standard_GRS' | 'Standard_RAGRS' | 'Standard_ZRS' | 'Premium_LRS' | 'Premium_ZRS'

@export()
type StorageAccountKind = 'Storage' | 'StorageV2' | 'BlobStorage' | 'FileStorage' | 'BlockBlobStorage'

@export()
type AccessTier = 'Hot' | 'Cool'

@export()
type TlsVersion = 'TLS1_0' | 'TLS1_1' | 'TLS1_2'

@export()
type NetworkAction = 'Allow' | 'Deny'

@export()
type EnableState = 'Disabled' | 'Enabled'

@export()
type StorageAccountConfig = {
  @description('Name of the storage account')
  storageAccountName: string
  
  @description('Location for the storage account')
  location: string?
  
  @description('Storage account SKU')
  skuName: StorageAccountSku?
  
  @description('Storage account kind')
  kind: StorageAccountKind?
  
  @description('Storage account access tier')
  accessTier: AccessTier?
  
  @description('Allow public access to blobs')
  allowBlobPublicAccess: bool?
  
  @description('Allow shared key access')
  allowSharedKeyAccess: bool?
  
  @description('Minimum TLS version')
  minimumTlsVersion: TlsVersion?
  
  @description('Enable HTTPS traffic only')
  supportsHttpsTrafficOnly: bool?
  
  @description('Network access configuration')
  networkAclsDefaultAction: NetworkAction?
  
  @description('Virtual network rules')
  virtualNetworkRules: array?
  
  @description('IP rules for firewall')
  ipRules: array?
  
  @description('Tags for the storage account')
  tags: object?
  
  @description('Enable hierarchical namespace (Data Lake Gen2)')
  isHnsEnabled: bool?
  
  @description('Large file shares state')
  largeFileSharesState: EnableState?
}

// Parameters
@description('Storage account configuration')
param storageAccountConfig StorageAccountConfig

// Direct resource creation (not using AVM to avoid drift noise)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountConfig.storageAccountName
  location: storageAccountConfig.?location ?? resourceGroup().location
  kind: storageAccountConfig.?kind ?? 'StorageV2'
  sku: {
    name: storageAccountConfig.?skuName ?? 'Standard_LRS'
  }
  tags: storageAccountConfig.?tags ?? {}
  properties: {
    accessTier: storageAccountConfig.?accessTier ?? 'Hot'
    allowBlobPublicAccess: storageAccountConfig.?allowBlobPublicAccess ?? false
    allowSharedKeyAccess: storageAccountConfig.?allowSharedKeyAccess ?? true
    minimumTlsVersion: storageAccountConfig.?minimumTlsVersion ?? 'TLS1_2'
    supportsHttpsTrafficOnly: storageAccountConfig.?supportsHttpsTrafficOnly ?? true
    isHnsEnabled: storageAccountConfig.?isHnsEnabled ?? false
    largeFileSharesState: storageAccountConfig.?largeFileSharesState ?? 'Disabled'
    networkAcls: {
      defaultAction: storageAccountConfig.?networkAclsDefaultAction ?? 'Allow'
      virtualNetworkRules: storageAccountConfig.?virtualNetworkRules ?? []
      ipRules: storageAccountConfig.?ipRules ?? []
      bypass: 'AzureServices'
    }
  }
}

@description('Storage account resource ID')
output storageAccountId string = storageAccount.id

@description('Storage account name')
output storageAccountName string = storageAccount.name

@description('Storage account primary blob endpoint')
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
