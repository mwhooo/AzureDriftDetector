// Type definitions
@export()
type KeyVaultSkuFamily = 'A'

@export()
type KeyVaultSkuName = 'standard' | 'premium'

@export()
type PublicAccess = 'Enabled' | 'Disabled'

@export()
type KeyVaultSku = {
  @description('SKU family')
  family: KeyVaultSkuFamily
  
  @description('SKU name')
  name: KeyVaultSkuName
}

@export()
type KeyVaultConfig = {
  @description('Key Vault name')
  name: string
  
  @description('Location for the Key Vault')
  location: string?
  
  @description('SKU configuration')
  sku: KeyVaultSku
  
  @description('Enable for deployment')
  enabledForDeployment: bool?
  
  @description('Enable for template deployment')
  enabledForTemplateDeployment: bool?
  
  @description('Enable for disk encryption')
  enabledForDiskEncryption: bool?
  
  @description('Enable RBAC authorization')
  enableRbacAuthorization: bool?
  
  @description('Public network access')
  publicNetworkAccess: PublicAccess?
  
  @description('Tags for the Key Vault')
  tags: object?
}

// Parameters
@description('Key Vault configuration')
param keyVaultConfig KeyVaultConfig

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultConfig.name
  location: keyVaultConfig.?location ?? resourceGroup().location
  properties: {
    sku: keyVaultConfig.sku
    tenantId: subscription().tenantId
    enabledForDeployment: keyVaultConfig.?enabledForDeployment ?? false
    enabledForTemplateDeployment: keyVaultConfig.?enabledForTemplateDeployment ?? false
    enabledForDiskEncryption: keyVaultConfig.?enabledForDiskEncryption ?? false
    enableRbacAuthorization: keyVaultConfig.?enableRbacAuthorization ?? true
    publicNetworkAccess: keyVaultConfig.?publicNetworkAccess ?? 'Enabled'
  }
  tags: keyVaultConfig.?tags ?? {}
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
