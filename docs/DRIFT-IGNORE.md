# Drift Ignore Configuration

The Azure Drift Detector includes a sophisticated ignore system to suppress false positive drift detections caused by Azure platform behaviors, managed properties, and other expected variations.

## Overview

Azure resources often have properties that are managed by the platform itself, such as timestamps, provisioning states, or properties that vary based on SKU tiers. The drift ignore system allows you to filter out these expected differences to focus on actual configuration drift.

## Configuration File Structure

The drift ignore configuration is stored in JSON format with the following structure:

```json
{
  "ignorePatterns": {
    "description": "Configuration to suppress known false positive drift detections",
    "resources": [
      {
        "resourceType": "Microsoft.ServiceBus/namespaces/queues",
        "reason": "Azure Service Bus Basic tier retains premium properties after deployment",
        "ignoredProperties": [
          "properties.autoDeleteOnIdle",
          "properties.defaultMessageTimeToLive", 
          "properties.duplicateDetectionHistoryTimeWindow",
          "properties.maxMessageSizeInKilobytes"
        ],
        "conditions": {
          "skuTier": "Basic"
        }
      }
    ],
    "globalPatterns": [
      {
        "propertyPattern": "properties.provisioningState",
        "reason": "Azure managed property that varies during deployment lifecycle"
      },
      {
        "propertyPattern": "properties.createdAt",
        "reason": "Azure managed timestamp property"
      }
    ]
  }
}
```

## Configuration Sections

### 1. Resource-Specific Rules (`resources`)

Target specific Azure resource types with conditional filtering:

- **`resourceType`**: The Azure resource type (e.g., `Microsoft.Storage/storageAccounts`)
- **`reason`**: Human-readable explanation for the ignore rule
- **`ignoredProperties`**: Array of property paths to ignore
- **`conditions`**: Optional conditions that must match for the rule to apply

### 2. Global Patterns (`globalPatterns`)

Apply ignore rules across all resource types:

- **`propertyPattern`**: Property path or pattern to ignore
- **`reason`**: Explanation for the global ignore rule

## Property Path Syntax

Property paths use dot notation to navigate the resource JSON structure:

```
properties.accessTier          # Simple property
properties.encryption.services # Nested property
sku.tier                       # SKU properties
tags.Environment              # Tag values
```

## Common Ignore Patterns

### Azure Platform Properties

```json
{
  "globalPatterns": [
    {
      "propertyPattern": "properties.provisioningState",
      "reason": "Azure managed provisioning lifecycle property"
    },
    {
      "propertyPattern": "properties.createdAt",
      "reason": "Azure managed creation timestamp"
    },
    {
      "propertyPattern": "properties.updatedAt",
      "reason": "Azure managed update timestamp"
    },
    {
      "propertyPattern": "properties.resourceGuid",
      "reason": "Azure generated unique identifier"
    }
  ]
}
```

### Storage Account Examples

```json
{
  "resources": [
    {
      "resourceType": "Microsoft.Storage/storageAccounts",
      "reason": "Azure managed storage properties",
      "ignoredProperties": [
        "properties.primaryEndpoints",
        "properties.statusOfPrimary",
        "properties.lastGeoFailoverTime"
      ]
    }
  ]
}
```

### Service Bus Examples

```json
{
  "resources": [
    {
      "resourceType": "Microsoft.ServiceBus/namespaces/queues",
      "reason": "Premium properties retained in Basic tier",
      "ignoredProperties": [
        "properties.autoDeleteOnIdle",
        "properties.defaultMessageTimeToLive",
        "properties.duplicateDetectionHistoryTimeWindow",
        "properties.maxMessageSizeInKilobytes"
      ],
      "conditions": {
        "skuTier": "Basic"
      }
    }
  ]
}
```

### Key Vault Examples

```json
{
  "resources": [
    {
      "resourceType": "Microsoft.KeyVault/vaults",
      "reason": "Azure managed Key Vault properties",
      "ignoredProperties": [
        "properties.vaultUri",
        "properties.hsmPoolResourceId"
      ]
    }
  ]
}
```

## Conditional Matching

The `conditions` object allows rules to apply only when specific conditions are met:

```json
{
  "resourceType": "Microsoft.Storage/storageAccounts",
  "ignoredProperties": ["properties.largeFileSharesState"],
  "conditions": {
    "sku.tier": "Standard",
    "kind": "StorageV2"
  }
}
```

## Usage Examples

### Command Line

```bash
# Use default drift-ignore.json in current directory
dotnet run -- --bicep-file template.bicep --resource-group myRG

# Use custom ignore configuration
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config custom-ignore.json

# Use ignore config from different directory
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config /path/to/ignore-config.json
```

### GitHub Actions

```yaml
- name: Check Drift
  run: |
    chmod +x .github/scripts/check-drift.sh
    .github/scripts/check-drift.sh "${{ matrix.environment.template }}" "${{ matrix.environment.resource_group }}" "drift-report.json" "custom-ignore.json"
```

## Best Practices

### 1. Document Your Ignore Rules

Always include meaningful `reason` fields to explain why properties are ignored:

```json
{
  "propertyPattern": "properties.encryption.keyVaultProperties.keyIdentifier",
  "reason": "Key Vault key identifier includes version which changes on rotation"
}
```

### 2. Be Specific

Prefer resource-specific rules over global patterns when possible:

```json
// Good - Specific to storage accounts
{
  "resourceType": "Microsoft.Storage/storageAccounts",
  "ignoredProperties": ["properties.primaryEndpoints.blob"]
}

// Less ideal - Too broad
{
  "propertyPattern": "properties.primaryEndpoints",
  "reason": "Endpoints vary"
}
```

### 3. Use Conditions for SKU-Dependent Properties

Many Azure services have different properties available based on pricing tier:

```json
{
  "resourceType": "Microsoft.Cache/Redis",
  "ignoredProperties": [
    "properties.redisConfiguration.maxclients",
    "properties.redisConfiguration.maxmemory-reserved"
  ],
  "conditions": {
    "sku.family": "C",
    "sku.name": "Basic"
  }
}
```

### 4. Regular Review

Periodically review your ignore rules to ensure they're still relevant:

- Remove rules for resources no longer in use
- Update rules when Azure service behaviors change
- Add new rules as you deploy new resource types

## Troubleshooting

### Debug Ignore Rules

The drift detector shows which ignores are being applied:

```
🔇 Ignoring drift: Microsoft.ServiceBus/namespaces/queues/myqueue - properties.autoDeleteOnIdle
🔇 Ignoring drift: Microsoft.Storage/storageAccounts/mystorage - properties.primaryEndpoints.blob
```

### Common Issues

1. **Property Path Mismatch**: Ensure property paths exactly match the JSON structure
2. **Condition Not Met**: Check that conditional values match exactly (case-sensitive)
3. **Resource Type Mismatch**: Verify resource type strings match Azure's format
4. **JSON Syntax**: Validate JSON syntax using a JSON validator

### Validation

Test your ignore configuration by running drift detection and observing the ignore messages:

```bash
# Run with verbose output to see ignore processing
dotnet run -- --bicep-file template.bicep --resource-group test --output Console
```

## Advanced Features

### Pattern Matching

Property patterns support basic wildcard matching:

```json
{
  "propertyPattern": "properties.*.timestamp",
  "reason": "Any timestamp property"
}
```

### Multiple Conditions

Conditions use AND logic - all must match:

```json
{
  "conditions": {
    "sku.tier": "Standard",
    "kind": "StorageV2",
    "properties.accessTier": "Hot"
  }
}
```

## File Location Priority

The drift detector searches for ignore configuration in this order:

1. Path specified by `--ignore-config` parameter
2. `drift-ignore.json` in current working directory
3. No ignore configuration (all drift reported)

## Example Complete Configuration

```json
{
  "ignorePatterns": {
    "description": "Production ignore patterns for Azure resources",
    "resources": [
      {
        "resourceType": "Microsoft.Storage/storageAccounts",
        "reason": "Azure managed storage endpoint properties",
        "ignoredProperties": [
          "properties.primaryEndpoints",
          "properties.secondaryEndpoints",
          "properties.statusOfPrimary",
          "properties.statusOfSecondary"
        ]
      },
      {
        "resourceType": "Microsoft.KeyVault/vaults",
        "reason": "Azure managed vault URI",
        "ignoredProperties": [
          "properties.vaultUri"
        ]
      }
    ],
    "globalPatterns": [
      {
        "propertyPattern": "properties.provisioningState",
        "reason": "Azure deployment lifecycle property"
      },
      {
        "propertyPattern": "properties.createdAt",
        "reason": "Azure managed timestamp"
      },
      {
        "propertyPattern": "properties.updatedAt",
        "reason": "Azure managed timestamp"
      },
      {
        "propertyPattern": "systemData",
        "reason": "Azure system metadata"
      }
    ]
  }
}
```

This configuration provides a solid foundation for most Azure deployments while filtering out common platform-managed properties.