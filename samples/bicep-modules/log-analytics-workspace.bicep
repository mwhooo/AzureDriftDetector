// Type definitions
@export()
type LogAnalyticsSku = 'Free' | 'Standard' | 'Premium' | 'PerNode' | 'PerGB2018' | 'Standalone'

@export()
type LogAnalyticsConfig = {
  @description('Log Analytics Workspace name')
  name: string
  
  @description('Location for the workspace')
  location: string?
  
  @description('SKU name')
  skuName: LogAnalyticsSku?
  
  @description('Data retention in days')
  @minValue(30)
  @maxValue(730)
  retentionInDays: int?
  
  @description('Enable log access using only resource permissions')
  enableLogAccessUsingOnlyResourcePermissions: bool?
  
  @description('Tags for the workspace')
  tags: object?
}

// Parameters
@description('Log Analytics Workspace configuration')
param logAnalyticsConfig LogAnalyticsConfig

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsConfig.name
  location: logAnalyticsConfig.?location ?? resourceGroup().location
  properties: {
    sku: {
      name: logAnalyticsConfig.?skuName ?? 'PerGB2018'
    }
    retentionInDays: logAnalyticsConfig.?retentionInDays ?? 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: logAnalyticsConfig.?enableLogAccessUsingOnlyResourcePermissions ?? true
    }
  }
  tags: logAnalyticsConfig.?tags ?? {}
}

output workspaceId string = logAnalyticsWorkspace.id
output workspaceName string = logAnalyticsWorkspace.name
output customerId string = logAnalyticsWorkspace.properties.customerId
