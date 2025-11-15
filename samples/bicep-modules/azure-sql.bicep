@description('Configuration for Azure SQL Database')
param sqlConfig object

@description('Location for the SQL server')
param location string = resourceGroup().location

@description('Resource tags')
param tags object = {}

@export()
type SqlDatabaseConfig = {
  serverName: string
  databaseName: string
  administratorLogin: string
  administratorLoginPassword: string
  sku: {
    name: 'Basic' | 'S0' | 'S1' | 'S2' | 'P1' | 'P2' | 'GP_Gen5_2' | 'GP_Gen5_4'
    tier: 'Basic' | 'Standard' | 'Premium' | 'GeneralPurpose'
  }
  maxSizeBytes: int?
  collation: string?
  firewallRules: {
    name: string
    startIpAddress: string
    endIpAddress: string
  }[]?
}

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlConfig.serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlConfig.administratorLogin
    administratorLoginPassword: sqlConfig.administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlConfig.databaseName
  location: location
  tags: tags
  sku: sqlConfig.sku
  properties: {
    maxSizeBytes: sqlConfig.maxSizeBytes
    collation: sqlConfig.collation ?? 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource firewallRules 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = [for rule in (sqlConfig.firewallRules ?? []): {
  parent: sqlServer
  name: rule.name
  properties: {
    startIpAddress: rule.startIpAddress
    endIpAddress: rule.endIpAddress
  }
}]

output serverId string = sqlServer.id
output databaseId string = sqlDatabase.id
output serverName string = sqlServer.name
output databaseName string = sqlDatabase.name
