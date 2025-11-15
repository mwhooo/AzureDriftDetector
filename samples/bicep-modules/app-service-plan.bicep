// Type definitions
@export()
type AppServicePlanSku = {
  @description('SKU name (e.g., F1, B1, S1, P1v2)')
  name: string
  
  @description('SKU tier (e.g., Free, Basic, Standard, Premium)')
  tier: string
}

@export()
type AppServicePlanConfig = {
  @description('App Service Plan name')
  name: string
  
  @description('Location for the App Service Plan')
  location: string?
  
  @description('SKU configuration for the App Service Plan')
  sku: AppServicePlanSku
  
  @description('Whether the App Service Plan is reserved (Linux)')
  reserved: bool?
  
  @description('Enable zone redundancy')
  zoneRedundant: bool?
  
  @description('Tags for the App Service Plan')
  tags: object?
}

// Parameters
@description('App Service Plan configuration')
param appServicePlanConfig AppServicePlanConfig

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanConfig.name
  location: appServicePlanConfig.?location ?? resourceGroup().location
  sku: appServicePlanConfig.sku
  properties: {
    reserved: appServicePlanConfig.?reserved ?? false
    zoneRedundant: appServicePlanConfig.?zoneRedundant ?? false
  }
  tags: appServicePlanConfig.?tags ?? {}
}

output appServicePlanId string = appServicePlan.id
output appServicePlanName string = appServicePlan.name
