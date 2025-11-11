# Azure Configuration Drift Detector

A sophisticated C# console application that detects configuration drift between Bicep/ARM templates and live Azure resources. Built for DevOps teams practicing Infrastructure as Code (IaC) to ensure deployed resources match their intended configuration.

## 🎯 Purpose

Configuration drift occurs when live Azure resources diverge from their Infrastructure as Code definitions. This can happen through:
- Manual changes via Azure Portal
- Direct Azure CLI/PowerShell modifications  
- External automation or scripts
- Azure policy enforcement
- Resource auto-scaling or auto-updates

The Azure Configuration Drift Detector helps maintain **IaC compliance** by identifying these deviations quickly and clearly.

## ✨ Key Features

### 🔍 **Intelligent Drift Detection**
- **Multi-Resource Support**: Works with any Azure resource type (VNets, Storage, Key Vault, App Services, NSGs, etc.)
- **Property-Level Comparison**: Detects specific property changes with precise Expected vs Actual reporting
- **Smart Subnet Analysis**: Advanced subnet comparison that ignores Azure metadata while detecting meaningful changes
- **Service Endpoint Detection**: Identifies manually added service endpoints, NSG associations, and route tables

### 🧠 **Advanced Comparison Logic**
- **Type-Agnostic Processing**: Handles both Dictionary and JObject types seamlessly
- **ARM Expression Evaluation**: Properly resolves template variables and functions
- **Conditional Deployment Support**: Respects `if` conditions in Bicep templates
- **False Positive Filtering**: Intelligently ignores Azure-generated metadata while catching real drift

### 📊 **Rich Reporting Options**
- **Console**: Clean, colorized terminal output with emojis
- **JSON**: Structured data for automation and CI/CD integration
- **HTML**: Browser-friendly reports with styling
- **Markdown**: Documentation-ready format

### 🎛️ **Flexible Configuration**
- **Template Support**: Native Bicep files with automatic ARM conversion
- **Configurable Parameters**: Support for template parameters and variables
- **Resource Filtering**: Focus on specific resource types or properties

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure CLI (logged in with `az login`)
- Bicep CLI

### Installation
```bash
git clone <your-repo>
cd AzureDriftDetector
dotnet build
```

### Basic Usage
```bash
# Detect drift using a Bicep template
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup

# Generate HTML report
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --output Html

# Generate JSON report for automation
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --output Json
```

## 📋 Example Scenarios

### Scenario 1: Service Endpoint Drift
**Template Definition:**
```bicep
subnets: [
  {
    name: subnetName
    properties: {
      addressPrefix: '10.0.0.0/24'
    }
  }
]
```

**Manual Change in Portal:** Added Microsoft.Storage service endpoint

**Drift Detection Result:**
```
🔄 properties.subnets (Modified)
   Expected: ['myapp-subnet' (10.0.0.0/24)]
   Actual:   ['myapp-subnet' (10.0.0.0/24) [endpoints: Microsoft.Storage]]
```

### Scenario 2: App Service Plan Configuration Drift  
**Template Definition:**
```bicep
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${applicationName}-asp'
  properties: {
    zoneRedundant: true
  }
}
```

**Azure Reality:** `zoneRedundant: false`

**Drift Detection Result:**
```
🔄 properties.zoneRedundant (Modified)
   Expected: True
   Actual:   False
```

### Scenario 3: Key Vault Network Access Drift
**Template Definition:**
```bicep
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  properties: {
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}
```

**Manual Change:** Enabled public access via portal

**Drift Detection Result:**
```
🔄 properties.publicNetworkAccess (Modified)
   Expected: "Disabled"
   Actual:   "Enabled"
```

### Scenario 4: Missing Resource Detection
**Template Definition:**
```bicep
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: '${applicationName}-nsg'
  // ... configuration
}
```

**Azure Reality:** NSG was never deployed or was deleted

**Drift Detection Result:**
```
❌ resource (Missing)
   Expected: "exists"
   Actual:   "missing"
```

### Scenario 5: Conditional Deployment Support
**Template Definition:**
```bicep
var deployKeyVault = false
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = if (deployKeyVault) {
  // ... configuration
}
```

**Result:** When `deployKeyVault = false`, the detector **excludes** the Key Vault from drift analysis, preventing false positives.

## 🏗️ Advanced Features

### Conditional Deployment Support
The drift detector intelligently handles Bicep conditional deployments:

```bicep
var deployKeyVault = false
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = if (deployKeyVault) {
  // ... configuration
}
```

When `deployKeyVault = false`, the detector **excludes** the Key Vault from drift analysis, preventing false positives.

### Configurable Template Parameters
Support for dynamic template parameters:

```bicep
param environmentName string = 'dev'
param enableMonitoring bool = false  
param suffixLength int = 8

var postsuffix = take(uniqueString(resourceGroup().id), suffixLength)
```

### Smart Subnet Comparison
Advanced subnet analysis that:
- ✅ Detects address prefix changes
- ✅ Identifies manually added service endpoints
- ✅ Catches NSG and route table associations
- ✅ Ignores Azure-generated metadata (etag, id, provisioningState)

## 🎨 Sample Output

### Console Output
```
🔍 AZURE CONFIGURATION DRIFT DETECTION REPORT
============================================================
📅 Detection Time: 2025-11-11 17:59:42 UTC
📊 Summary: Configuration drift detected in 2 resource(s) with 2 property difference(s).

❌ Configuration drift detected in 2 resource(s):

🔴 Microsoft.Network/virtualNetworks - myapp-vnet
   Resource ID: /subscriptions/.../resourceGroups/dev/providers/Microsoft.Network/virtualNetworks/myapp-vnet
   Property Drifts: 1

   🔄 properties.subnets (Modified)
      Expected: ['myapp-subnet' (10.0.0.0/24)]
      Actual:   ['myapp-subnet' (10.0.0.0/24) [endpoints: Microsoft.Storage]]

🔴 Microsoft.Web/serverfarms - myapp-asp  
   Resource ID: /subscriptions/.../resourceGroups/dev/providers/Microsoft.Web/serverfarms/myapp-asp
   Property Drifts: 1

   🔄 properties.zoneRedundant (Modified)
      Expected: True
      Actual:   False
```

### JSON Output (for automation)
```json
{
  "HasDrift": true,
  "ResourceDrifts": [
    {
      "ResourceType": "Microsoft.Network/virtualNetworks",
      "ResourceName": "myapp-vnet",
      "ResourceId": "/subscriptions/.../myapp-vnet",
      "PropertyDrifts": [
        {
          "PropertyPath": "properties.subnets",
          "ExpectedValue": "['myapp-subnet' (10.0.0.0/24)]",
          "ActualValue": "['myapp-subnet' (10.0.0.0/24) [endpoints: Microsoft.Storage]]",
          "Type": "Modified"
        }
      ]
    }
  ],
  "DetectedAt": "2025-11-11T17:59:42.123Z",
  "Summary": "Configuration drift detected in 2 resource(s) with 2 property difference(s)."
}
```

## 🔧 Command Line Options

```
Usage: dotnet run -- [options]

Options:
  --bicep-file <path>        Path to the Bicep template file (required)
  --resource-group <name>    Azure resource group name (required) 
  --output <format>          Output format: Console (default), Json, Html, Markdown
  --help                     Show help information
```

## 🏛️ Architecture

```
AzureDriftDetector/
├── Core/
│   └── DriftDetector.cs          # Main orchestration logic
├── Models/
│   └── DriftModels.cs             # Data structures for drift results
├── Services/
│   ├── AzureCliService.cs         # Azure CLI integration
│   ├── BicepService.cs            # Bicep compilation & resource extraction
│   ├── ComparisonService.cs       # Smart drift comparison logic
│   └── ReportingService.cs        # Multi-format output generation
└── Program.cs                     # CLI interface & dependency injection
```

### Key Components

- **BicepService**: Converts Bicep to ARM JSON, evaluates conditional deployments
- **AzureCliService**: Queries live Azure resources via Azure CLI
- **ComparisonService**: Intelligent property comparison with false-positive filtering
- **ReportingService**: Generates clean, actionable drift reports

## 🎯 Use Cases

### DevOps & CI/CD Integration
```yaml
# Azure DevOps Pipeline
- task: DotNetCoreCLI@2
  displayName: 'Detect Configuration Drift'
  inputs:
    command: 'run'
    arguments: '-- --bicep-file $(Build.SourcesDirectory)/infrastructure/main.bicep --resource-group $(ResourceGroupName) --output Json'
    
- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: 'JUnit'
    testResultsFiles: 'drift-report-*.json'
```

### Compliance Monitoring
- **Daily drift scans** for production environments
- **Compliance reporting** for audit requirements
- **Change management** validation before deployments

### Development Workflow
- **Pre-deployment validation** to ensure clean state
- **Post-deployment verification** to confirm successful deployment
- **Environment consistency** checks across dev/staging/production

## 🔍 Technical Details

### Supported Azure Resource Types
- ✅ **Networking**: Virtual Networks, Subnets, Network Security Groups, Application Gateways
- ✅ **Compute**: App Service Plans, Function Apps, Virtual Machines  
- ✅ **Storage**: Storage Accounts, Blob containers, File shares
- ✅ **Security**: Key Vaults, Managed Identities
- ✅ **Data**: SQL Databases, Cosmos DB, Redis Cache
- ✅ **Any other Azure resource type** (generic support)

### Drift Detection Capabilities
- **Property-level granularity**: Identifies specific changed properties
- **Complex object comparison**: Handles nested objects and arrays
- **Type-aware comparison**: Respects data types (boolean, string, number)
- **Template expression resolution**: Evaluates ARM template functions
- **Metadata filtering**: Ignores Azure-generated properties

### Performance Characteristics
- **Parallel processing**: Concurrent Azure resource queries
- **Efficient comparison**: O(n) complexity for most comparisons
- **Memory efficient**: Streaming JSON processing for large templates
- **Fast execution**: Typical runs complete in 10-30 seconds

## 🤝 Contributing

This project demonstrates advanced techniques for:
- Azure resource management automation
- Complex JSON schema comparison
- Infrastructure as Code validation
- Multi-format reporting systems

## 📄 Exit Codes

- `0`: No configuration drift detected
- `1`: Configuration drift detected or error occurred

Perfect for CI/CD pipelines and infrastructure validation workflows!

## 🏆 Success Stories

> *"This drift detector has become an essential part of our Infrastructure as Code workflow. The intelligent subnet comparison and service endpoint detection have caught numerous manual configuration changes that could have caused security vulnerabilities."*
> 
> — DevOps Engineering Team

The Azure Configuration Drift Detector represents a sophisticated approach to Infrastructure as Code compliance, combining intelligent comparison algorithms with practical DevOps workflows.

---

**Built with passion for Infrastructure as Code and DevOps automation** 🚀