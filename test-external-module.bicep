// External AVM module from Azure Container Registry
module storageModule 'br:pebiceptemplates.azurecr.io/bicep/storage/storageaccount:v1.1.0' = {
  params: {
    config: {
      name: 'markstor232340934'
      location: 'uksouth'
      storageAccountName: 'markstor232340934'
      sku: 'Standard_LRS'
      kind: 'StorageV2'
      accessTier: 'Hot'
      systemAssignedIdentity: true
      allowBlobPublicAccess: false
      bypass: 'AzureServices'
      defaultSharePermission: 'None'
      directoryServiceOptions: 'AADKERB'
      tags: {
        environment: 'test'
      }
    }
  }
}

// Direct Azure resource - Network Security Group
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'test-nsg-mixed-scenario'
  location: 'uksouth'
  properties: {
    securityRules: [
      {
        name: 'allow-http-inbound'
        properties: {
          description: 'Allow HTTP traffic inbound'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1000
          direction: 'Inbound'
        }
      }
      {
        name: 'allow-https-inbound'
        properties: {
          description: 'Allow HTTPS traffic inbound'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1001
          direction: 'Inbound'
        }
      }
      {
        name: 'allow-ssh-inbound'
        properties: {
          description: 'Allow SSH access from management subnet'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '22'
          sourceAddressPrefix: '10.0.1.0/24'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1002
          direction: 'Inbound'
        }
      }
      {
        name: 'deny-all-inbound'
        properties: {
          description: 'Deny all other inbound traffic'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 4096
          direction: 'Inbound'
        }
      }
    ]
  }
  tags: {
    environment: 'test'
    purpose: 'mixed-scenario-testing'
  }
}

// Direct Azure resource - Public IP
resource publicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = {
  name: 'test-pip-mixed-scenario'
  location: 'uksouth'
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
    idleTimeoutInMinutes: 4
    dnsSettings: {
      domainNameLabel: 'test-mixed-scenario-${uniqueString(resourceGroup().id)}'
    }
  }
  tags: {
    environment: 'test'
    purpose: 'mixed-scenario-testing'
  }
}
