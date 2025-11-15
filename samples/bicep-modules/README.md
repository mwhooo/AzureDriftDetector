# 🧩 Bicep Module Library

This directory contains reusable Bicep modules for common Azure resources. Each module is designed to be composable, type-safe, and follows Azure best practices.

## 📁 Available Modules

### Infrastructure Modules
- **`storage-account.bicep`** - Azure Storage Account with configurable SKU and features
- **`key-vault.bicep`** - Azure Key Vault with RBAC and access policies
- **`virtual-network.bicep`** - Virtual Network with subnets and security groups
- **`network-security-group.bicep`** - Network Security Groups with rule sets
- **`log-analytics-workspace.bicep`** - Log Analytics Workspace for monitoring

### Compute & Platform Modules  
- **`app-service-plan.bicep`** - App Service Plan with scaling options
- **`application-gateway.bicep`** - Application Gateway with SSL and routing
- **`service-bus.bicep`** - Service Bus with queues and topics

### Data & Analytics Modules
- **`azure-sql.bicep`** - Azure SQL Database with security configurations

## 🏗️ Architecture Principles

### Type-Safe Design
Each module exports its own configuration types using `@export()`:

```bicep
@export()
type StorageAccountConfig = {
  name: string
  sku: 'Standard_LRS' | 'Standard_GRS' | 'Premium_LRS'
  containers?: string[]
  // ... more properties
}
```

### Single Config Parameter
Modules accept one primary config object plus common parameters:

```bicep
param config StorageAccountConfig
param location string = resourceGroup().location
param tags object = {}
```

### Import and Use
Import types in your main template:

```bicep
import { StorageAccountConfig } from 'bicep-modules/storage-account.bicep'

param storageConfig StorageAccountConfig = {
  name: 'mystorageaccount'
  sku: 'Standard_LRS'
}

module storage 'bicep-modules/storage-account.bicep' = {
  name: 'storage-deployment'
  params: {
    config: storageConfig
    location: location
    tags: tags
  }
}
```

## 🔧 Development Guidelines

### Module Structure
```bicep
// 1. Type definitions with @export()
@export()
type ModuleConfig = {
  // Configuration properties
}

// 2. Parameters
param config ModuleConfig
param location string = resourceGroup().location
param tags object = {}

// 3. Variables (internal logic)
var someVariable = config.someProperty

// 4. Resources
resource myResource 'Microsoft.Resource/type@api-version' = {
  // Resource definition
}

// 5. Outputs
output resourceId string = myResource.id
output resourceName string = myResource.name
```

### Best Practices

1. **Type Safety**: Always use union types for enum-like properties
2. **Defaults**: Provide sensible defaults where possible
3. **Validation**: Use parameter validation decorators
4. **Documentation**: Include description decorators for parameters
5. **Modularity**: Keep modules focused on single resource types
6. **Outputs**: Export useful resource properties for referencing

### Testing
Test modules using the sample templates in [`../samples/`](../samples/):

```bash
# Test a specific module
az deployment group what-if \
  --resource-group test-rg \
  --template-file samples/main-template.bicep \
  --parameters samples/main-template.bicepparam
```

## 📚 Related Documentation

- [Sample Templates](../samples/README.md) - Example usage of these modules
- [Drift Detection Guide](../docs/DRIFT-IGNORE.md) - Configure drift detection for module resources
- [Bicep Build Guide](../docs/BICEP-BUILD.md) - Module development and build process

## 🤝 Contributing

When adding new modules:

1. **Follow naming convention**: `resource-type.bicep`
2. **Export configuration types** with `@export()`
3. **Use consistent parameter patterns** (config, location, tags)
4. **Add comprehensive examples** in the samples directory
5. **Update this README** with module description
6. **Test thoroughly** before submitting

## 🔗 References

- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Bicep Type System](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/data-types)
- [Azure Resource Reference](https://docs.microsoft.com/en-us/azure/templates/)