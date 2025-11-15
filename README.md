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

### 🔍 **Azure What-If Based Drift Detection**
- **Authoritative Accuracy**: Uses Azure's native `az deployment group what-if` for 100% accurate drift detection
- **Zero False Positives**: Eliminates false positives from ARM expression comparisons
- **Multi-Resource Support**: Works with any Azure resource type (VNets, Storage, Key Vault, App Services, NSGs, etc.)
- **Property-Level Comparison**: Detects specific property changes with precise Expected vs Actual reporting
- **Complex Object Handling**: Intelligent reporting for arrays and nested objects

### 🎨 **Type-Safe Bicep with User-Defined Types (UDTs)**
- **Exported Types**: Each Bicep module exports its own configuration types with `@export()`
- **Single Config Objects**: Clean module interface with one config parameter per module
- **Full IntelliSense**: Complete type checking and autocomplete in VS Code
- **DRY Architecture**: Types defined once in modules, imported where needed
- **Compile-Time Validation**: Catch configuration errors before deployment

### 📊 **Clean, Human-Friendly Reporting**
- **Suppressed Verbose Output**: Azure what-if output hidden, showing only formatted results
- **Console**: Clean, colorized terminal output with emojis
- **JSON**: Structured data for automation and CI/CD integration
- **HTML**: Browser-friendly reports with styling
- **Markdown**: Documentation-ready format
- **Complex Object Messages**: Clear explanations for array/object drift instead of raw JSON

### 🔧 **Automatic Drift Remediation**
- **Autofix Mode**: Automatically deploy Bicep template to fix detected drift with `--autofix` flag
- **Smart Deployment**: Only deploys when actual drift is detected
- **Safe Execution**: Provides detailed deployment feedback and error handling
- **Deployment Tracking**: Generates unique deployment names with timestamps

### 🎛️ **Modern Bicep Architecture**
- **Modular Design**: Separate modules for each resource type in `bicep-modules/` directory
- **Bicepparam Support**: Native `.bicepparam` file support for parameter management
- **Union Types**: Type-safe SKU and configuration options using union types
- **Optional Parameters**: Nullable fields with safe access operators and sensible defaults
- **Parameter Merging**: Automatic merging of common parameters (location, tags) with config objects

### 🔇 **Intelligent Drift Filtering**
- **Noise Suppression**: Advanced ignore system to filter out Azure platform behaviors and false positives
- **Resource-Specific Rules**: Target specific resource types with conditional filtering
- **Global Patterns**: Apply ignore rules across all resource types for common Azure properties
- **Conditional Logic**: Rules that apply only when specific conditions are met (SKU tier, resource kind, etc.)
- **Configurable Paths**: Support for custom ignore configuration files via `--ignore-config`
- **Pattern Matching**: Flexible property path matching with wildcards and nested object support
- **Clear Feedback**: Visual indicators showing which drifts are being ignored and why

📖 **[Complete Drift Ignore Documentation](docs/DRIFT-IGNORE.md)** - Comprehensive guide with examples and best practices

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

# Detect drift using a Bicepparam file
dotnet run -- --bicep-file template.bicepparam --resource-group myResourceGroup

# Detect drift and automatically fix it
dotnet run -- --bicep-file template.bicepparam --resource-group myResourceGroup --autofix

# Generate HTML report
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --output Html

# Generate JSON report for automation
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --output Json

# Use custom ignore configuration to suppress Azure platform noise
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --ignore-config custom-ignore.json

# See docs/DRIFT-IGNORE.md for comprehensive ignore configuration guide
```

> 📖 **Need to configure drift ignore rules?** See our comprehensive [Drift Ignore Configuration Guide](docs/DRIFT-IGNORE.md) with examples for common Azure services and best practices.

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

### Scenario 2: Network Security Group Rule Drift
**Template Definition:**
```bicep
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: 'myapp-nsg'
  properties: {
    securityRules: [
      {
        name: 'AllowHTTP'
        priority: 100
        access: 'Allow'
        direction: 'Inbound'
        protocol: 'Tcp'
        sourcePortRange: '*'
        destinationPortRange: '80'
      }
    ]
  }
}
```

**Manual Change in Portal:** Added SSH rule with priority 200

**Drift Detection Result:**
```
🔄 properties.securityRules (Modified)
   Expected: "configured in template"
   Actual:   "differs in Azure (complex object/array)"
```

### Scenario 3: Storage Account Tag Drift
**Template Definition:**
```bicep
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  tags: {
    Environment: 'test'
    Application: 'drifttest'
    ResourceType: 'Infrastructure'
  }
}
```

**Manual Change:** Tags modified to `Environment: 'production'` and added `ManualTag: 'test'`

**Drift Detection Result:**
```
🔄 tags.Environment (Modified)
   Expected: "test"
   Actual:   "production"

❓ tags.ManualTag (Added)
   Expected: "not set"
   Actual:   "test"

❌ tags.ResourceType (Missing)
   Expected: "Infrastructure"
   Actual:   "removed"
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

### Scenario 5: Automatic Drift Remediation with --autofix
**Template Definition:**
```bicep
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: '${applicationName}-nsg'
  properties: {
    securityRules: [
      {
        name: 'AllowHTTP'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          access: 'Allow'
          direction: 'Inbound'
          priority: 100
        }
      }
    ]
  }
}
```

**Manual Change:** Added SSH rule via Azure Portal

**Drift Detection with Autofix:**
```bash
dotnet run -- --bicep-file template.bicep --resource-group myRG --autofix
```

**Output:**
```
❌ Configuration drift detected!
🔧 Attempting to fix drift by deploying template...
🚀 Deploying Bicep template to resource group: myRG
✅ Deployment completed successfully!
✅ Drift has been automatically fixed!
📦 Deployment Name: drift-autofix-20251113-150351
```

### Scenario 6: Conditional Deployment Support
**Template Definition:**
```bicep
var deployKeyVault = false
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = if (deployKeyVault) {
  // ... configuration
}
```

**Result:** When `deployKeyVault = false`, the detector **excludes** the Key Vault from drift analysis, preventing false positives.

## 🔇 Drift Ignore Configuration

The drift detection system includes a comprehensive ignore mechanism to suppress false positives caused by Azure platform behaviors beyond your control.

### Purpose
The ignore functionality is specifically designed to filter out "noise" from:
- **Azure Resource Manager (ARM)** automatically adding platform-managed properties
- **Azure Verified Modules (AVM)** modifying resources during or after deployment
- **Azure platform services** updating timestamps, provisioning states, capacity metrics, or internal references
- **Tier-specific behaviors** where Basic/Free tiers don't support certain properties that Premium tiers do
- **Platform-managed state** that occurs outside of your Bicep template configuration and control

### When to Use Ignore Patterns
✅ **Use for platform behaviors you cannot control:**
- Azure-managed timestamps (`lastModified`, `createdOn`, etc.)
- Provisioning states that change automatically
- Service tier limitations (Basic Service Bus not supporting advanced properties)
- Azure policy enforcement adding required tags/properties
- Auto-scaling metrics and capacity values

❌ **Don't ignore legitimate configuration drift:**
- Manual changes made via Azure Portal
- Security configuration modifications
- Resource property changes that should be in your template
- Actual configuration drift that indicates compliance issues

### Configuration Format
Ignore patterns are defined in JSON configuration files (default: `drift-ignore.json`):

```json
{
  "ignorePatterns": {
    "description": "Suppress Azure platform noise and false positives",
    "resources": [
      {
        "resourceType": "Microsoft.ServiceBus/namespaces/queues",
        "reason": "Service Bus Basic tier doesn't support these properties - Azure platform behavior",
        "ignoredProperties": [
          "properties.autoDeleteOnIdle",
          "properties.defaultMessageTimeToLive",
          "properties.duplicateDetectionHistoryTimeWindow",
          "properties.maxMessageSizeInKilobytes"
        ]
      }
    ],
    "globalPatterns": [
      {
        "propertyPattern": "properties.provisioningState", 
        "reason": "Azure-managed provisioning state - not user configurable"
      },
      {
        "propertyPattern": "properties.*Time*",
        "reason": "Ignore all Azure-managed timestamp properties"
      },
      {
        "propertyPattern": "properties.*time*",
        "reason": "Ignore all Azure-managed lowercase timestamp properties"
      }
    ]
  }
}
```

### Command Line Usage
```bash
# Use default ignore config (drift-ignore.json in current directory)
dotnet run -- --bicep-file template.bicep --resource-group myRG

# Use custom ignore configuration file
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config prod-ignore.json

# Use ignore config from different directory
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config configs/ignore.json
```

### Pattern Matching Rules
- **Exact Match**: `"properties.autoDeleteOnIdle"` matches exactly that property path
- **Wildcards**: `"properties.*Time*"` matches any property containing "Time" (case-sensitive)
- **Resource Types**: Support wildcards like `"Microsoft.ServiceBus/*"` for all Service Bus resource types
- **Global vs Resource-Specific**: Global patterns apply to all resources, resource-specific patterns only apply to matching resource types

### Real-World Example
Before implementing ignore patterns:
```
❌ Configuration drift detected in 13 resource(s) with 15 property difference(s).

🔴 Microsoft.ServiceBus/namespaces/queues - myqueue
   ❌ properties.autoDeleteOnIdle (Missing)
      Expected: "PT10675199DT2H48M5.4775807S"
      Actual:   null
   ❌ properties.defaultMessageTimeToLive (Missing) 
      Expected: "P14D"
      Actual:   null
   ❌ properties.maxMessageSizeInKilobytes (Missing)
      Expected: 1024
      Actual:   null
```

After implementing ignore patterns:
```
✅ No configuration drift detected!
📋 Filtered 12 ignored property differences
🎯 Focus on legitimate drift - noise suppressed
```

## 🏗️ Advanced Features

### Azure What-If Integration
The drift detector leverages Azure's native what-if functionality for authoritative drift detection:

```bash
# Behind the scenes, the tool runs:
az deployment group what-if --resource-group dev --template-file samples/main-template.bicep --parameters samples/main-template.bicepparam
```

This provides:
- ✅ **100% Accurate Results**: Uses Azure's deployment engine for comparison
- ✅ **No False Positives**: ARM expression evaluation handled by Azure
- ✅ **Comprehensive Analysis**: Detects all types of configuration changes
- ✅ **Clean Output**: Verbose what-if output suppressed, showing only formatted drift results

### Type-Safe Bicep Modules
Modern Bicep architecture with exported types:

```bicep
// bicep-modules/storage-account.bicep
@export()
type StorageAccountSku = 'Standard_LRS' | 'Standard_GRS' | 'Premium_LRS'

@export()
type StorageAccountConfig = {
  storageAccountName: string
  location: string?
  skuName: StorageAccountSku?
  // ... more fields
}

param storageAccountConfig StorageAccountConfig

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountConfig.storageAccountName
  location: storageAccountConfig.?location ?? resourceGroup().location
  // ...
}
```

```bicep
// main-template.bicep
import {StorageAccountConfig} from 'bicep-modules/storage-account.bicep'

param storageConfig StorageAccountConfig

module storageModule 'bicep-modules/storage-account.bicep' = {
  params: {
    storageAccountConfig: union(storageConfig, {location: location, tags: tags})
  }
}
```

### Bicepparam File Support
Clean parameter management with `.bicepparam` files:

```bicep
// main-template.bicepparam
using 'main-template.bicep'

param storageConfig = {
  storageAccountName: 'mystorageacct'
  skuName: 'Standard_LRS'
  kind: 'StorageV2'
  minimumTlsVersion: 'TLS1_2'
}

param tags = {
  Environment: 'production'
  Application: 'myapp'
}
```

## 🎨 Sample Output

### Console Output
```
🔍 AZURE CONFIGURATION DRIFT DETECTION REPORT
============================================================
📅 Detection Time: 2025-11-13 18:41:37 UTC
📊 Summary: Configuration drift detected in 2 resource(s) with 4 property difference(s).

❌ Configuration drift detected in 2 resource(s):

🔴 Microsoft.Storage/storageAccounts - drifttestsay6kt676i
   Resource ID:
   Property Drifts: 3

   🔄 tags.environment (Modified)
      Expected: "test"
      Actual:   "production"

   ❓ tags.manualTag (Added)
      Expected: "not set"
      Actual:   "drift"

   ❌ tags.Application (Missing)
      Expected: "drifttest"
      Actual:   "removed"

🔴 Microsoft.Network/networkSecurityGroups - drifttest-nsg
   Resource ID:
   Property Drifts: 1

   🔄 properties.securityRules (Modified)
      Expected: "configured in template"
      Actual:   "differs in Azure (complex object/array)"
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

## �️ Security & Quality Assurance

### GitHub Advanced Security
This project uses GitHub's security features to ensure code quality and security:

- **CodeQL Analysis**: Automated security vulnerability scanning
- **Dependency Scanning**: Monitors for vulnerable dependencies
- **Secret Scanning**: Prevents accidental credential commits

**Note**: For private repositories, GitHub Advanced Security requires enabling through repository settings. See [Security Setup Guide](docs/SECURITY-SETUP.md) for detailed instructions.

### Automated CI/CD Pipeline
Every push triggers comprehensive validation:
- ✅ Cross-platform builds (Ubuntu, macOS, Windows)
- ✅ Code quality and formatting checks
- ✅ Bicep template validation
- ✅ Security analysis with CodeQL
- ✅ Automated dependency updates

## 🔧 Command Line Options

```
Usage: dotnet run -- [options]

Options:
  --bicep-file <path>        Path to the Bicep template file (required)
  --resource-group <name>    Azure resource group name (required) 
  --output <format>          Output format: Console (default), Json, Html, Markdown
  --autofix                  Automatically deploy template to fix detected drift
  --ignore-config <path>     Path to drift ignore configuration file (default: drift-ignore.json)
  --simple-output           Use simple ASCII characters for CI/CD compatibility
  --help                     Show help information
```

## 🏛️ Architecture

```
AzureDriftDetector/
├── Core/
│   └── DriftDetector.cs          # Main orchestration logic with ignore integration
├── Models/
│   └── DriftModels.cs             # Data structures for drift results and ignore config
├── Services/
│   ├── AzureCliService.cs         # Azure CLI integration & deployments
│   ├── BicepService.cs            # Bicep compilation & what-if integration
│   ├── ComparisonService.cs       # What-if output parsing & drift analysis with filtering
│   ├── DriftIgnoreService.cs      # Ignore pattern matching and drift filtering
│   └── ReportingService.cs        # Multi-format output generation
├── bicep-modules/                 # Modular Bicep templates
│   ├── storage-account.bicep      # Storage with exported types
│   ├── virtual-network.bicep      # VNet with exported types
│   ├── network-security-group.bicep
│   ├── app-service-plan.bicep
│   ├── log-analytics-workspace.bicep
│   └── key-vault.bicep
├── docs/                          # Documentation
│   ├── DRIFT-IGNORE.md           # Comprehensive drift ignore configuration guide
│   ├── SECURITY-SETUP.md         # GitHub Advanced Security setup
│   └── BICEP-BUILD.md            # Bicep module development guide
├── drift-ignore.json              # Default ignore configuration for Azure platform noise
├── samples/                       # Example Bicep templates and parameters
│   ├── main-template.bicep        # Main template importing module types
│   └── main-template.bicepparam   # Parameter configuration
└── Program.cs                     # CLI interface & dependency injection
```

### Key Components

- **BicepService**: Integrates Azure what-if for authoritative drift detection, handles bicepparam files
- **AzureCliService**: Queries live Azure resources and executes deployments with proper error handling
- **ComparisonService**: Parses what-if text output into structured drift results with ignore filtering integration
- **DriftIgnoreService**: Pattern matching engine for filtering Azure platform noise and false positives
- **ReportingService**: Generates clean, actionable drift reports in multiple formats
- **Bicep Modules**: Type-safe, reusable infrastructure components with exported configuration types

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
- ✅ **Messaging**: Service Bus (Namespaces, Queues, Topics, Subscriptions)
- ✅ **Security**: Key Vaults, Managed Identities
- ✅ **Data**: SQL Databases, Cosmos DB, Redis Cache
- ✅ **Any other Azure resource type** (generic support)

### Drift Detection Capabilities
- **Azure What-If Based**: Uses Azure's native deployment engine for 100% accurate drift detection
- **Property-level granularity**: Identifies specific changed properties
- **Complex object support**: Handles arrays, nested objects with human-friendly messages
- **Tag drift detection**: Detects added, removed, and modified tags
- **Zero false positives**: Eliminates ARM expression comparison issues

### Performance Characteristics
- **Fast what-if execution**: Leverages Azure's optimized what-if engine
- **Clean output**: Suppressed verbose Azure output for better UX
- **Memory efficient**: Streaming text processing for what-if results
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

## 📄 Exit Codes

- `0`: No configuration drift detected
- `1`: Configuration drift detected or error occurred

Perfect for CI/CD pipelines and infrastructure validation workflows!

## 📝 Changelog

### v3.2.0 (2025-11-15) - Comprehensive Drift Ignore System 🔇
**Production-Ready Noise Reduction**

#### 🔇 **Drift Ignore System**
- ✨ **Configurable Ignore Patterns**: JSON-based configuration for suppressing Azure platform noise
- 🎯 **Purpose-Built for AVM/Platform Behaviors**: Specifically designed to filter out Azure Resource Manager and Azure Verified Module modifications beyond user control
- 📋 **Resource-Specific Rules**: Target specific resource types with custom ignore patterns
- 🌐 **Global Pattern Support**: Wildcards and pattern matching for broad timestamp/state filtering
- 🖥️ **Command Line Integration**: `--ignore-config` parameter for flexible configuration file paths
- 📊 **Filtering Statistics**: Reports showing how many false positives were suppressed

#### 🚀 **Enhanced Azure Resource Support**
- ✨ **Service Bus Integration**: Complete support for Namespaces, Queues, Topics, and Subscriptions
- 🔧 **Tier-Aware Configuration**: Conditional properties based on Basic vs Standard/Premium Service Bus tiers
- 🌐 **Application Gateway Module**: Full Azure Application Gateway Bicep module with exported types
- 🗃️ **Azure SQL Database**: Complete SQL Database support with server and database configuration

#### ⚡ **Improved Drift Detection Accuracy**
- 🐛 **Fixed What-If Symbol Interpretation**: Correctly handle -, +, ~, = symbols in Azure what-if output
- 🔍 **Better Child Resource Handling**: Enhanced detection for nested resources and complex object drift
- 📈 **Real-World Impact**: Reduced false positives from 13 to 1 in production scenarios

#### 🛠️ **Technical Architecture**
- 🆕 **DriftIgnoreService**: New service for pattern matching and drift filtering
- 🔧 **Enhanced ComparisonService**: Integrated ignore filtering with what-if parsing
- 📚 **Updated Documentation**: Comprehensive ignore configuration guide and best practices

### v3.0.0 (2025-11-13) - Major Architecture Overhaul 🚀
**Breaking Changes - Major Release**

#### 🎯 Azure What-If Integration
- ✨ **Authoritative Drift Detection**: Migrated from manual JSON comparison to Azure's native `az deployment group what-if` command
- ✅ **Zero False Positives**: Eliminated all false positives from ARM expression vs resolved value comparisons
- 🎨 **Clean Output**: Suppressed verbose Azure what-if output, showing only formatted drift results
- 📊 **Better Complex Object Handling**: Human-friendly messages for array/object drift instead of raw JSON snippets

#### 🏗️ Type-Safe Bicep Architecture
- ✨ **User-Defined Types (UDTs)**: Full Bicep type system with `@export()` decorators on all modules
- 📦 **Single Config Objects**: Each module accepts one config parameter instead of multiple individual params
- 🔧 **Modular Structure**: Separated all resources into `bicep-modules/` directory with exported types
- 🎯 **DRY Principle**: Types defined once in modules, imported in main template - no duplication
- ✅ **Compile-Time Validation**: Full IntelliSense and type checking for all Bicep files

#### 📁 Bicepparam Support
- ✨ **Native .bicepparam Files**: Full support for Bicep parameter files with `using` statements
- 🔍 **Automatic Reference Resolution**: Extracts referenced template from bicepparam files
- ⚡ **Streamlined Parameters**: Clean parameter management separate from template logic

#### 🎨 Enhanced User Experience
- 📊 **Improved Drift Messages**: Clear "configured in template" vs "differs in Azure (complex object/array)" for complex changes
- 🧹 **Removed Duplicate Code**: Eliminated redundant comparison logic in favor of what-if parsing
- ⚡ **Faster Execution**: What-if-based approach is faster than manual JSON traversal
- 🎯 **Accurate Tag Detection**: Precise detection of tag additions, removals, and modifications

#### 🔧 Technical Improvements
- 🏗️ **Refactored BicepService**: Now integrates what-if instead of building ARM templates
- 📝 **Enhanced ComparisonService**: Parses what-if text output into structured drift results
- 🧪 **Process Management**: Fixed deployment deadlock issues with proper stdout/stderr handling
- 🗂️ **Module Organization**: Clean separation of concerns with typed module interfaces

#### 📚 Module Updates
- Storage Account: Exported `StorageAccountConfig`, `StorageAccountSku`, `TlsVersion`, etc.
- Virtual Network: Exported `VnetConfig`, `Subnet`, `EnableState`
- NSG: Exported `NsgConfig`, `SecurityRule`, `AccessType`, `TrafficDirection`, `NetworkProtocol`
- App Service Plan: Exported `AppServicePlanConfig`, `AppServicePlanSku`
- Log Analytics: Exported `LogAnalyticsConfig`, `LogAnalyticsSku`
- Key Vault: Exported `KeyVaultConfig`, `KeyVaultSku`, `PublicAccess`

#### 🗑️ Removed
- ❌ Removed `types.bicep` - types now live with their modules (DRY principle)
- ❌ Removed manual JSON comparison logic - replaced with what-if parsing
- ❌ Removed `--simple-output` flag - no longer needed with clean what-if output

### v2.1.0 (2025-11-11) - Major Accuracy Improvements
- ✨ **Enhanced Comparison Logic**: Specialized handlers for NSG security rules, subnet arrays, and Log Analytics workspaces
- 🐛 **False Positive Elimination**: Intelligent filtering of Azure-generated metadata (provisioningState, etag, id, etc.)
- 🎨 **Improved JSON Formatting**: Human-readable console output with proper indentation and formatting
- 🔍 **Smart Array Detection**: Automatic detection and specialized comparison for different array types
- ⚡ **Performance Optimizations**: More efficient comparison algorithms for complex nested objects
- 📊 **Enhanced Reporting**: Better formatting for console output with base JSON formatting
- 🧪 **Comprehensive Testing**: Validated with real Azure resources across multiple resource types

### Previous Versions
- v2.0.0 - Initial stable release with multi-resource support
- v1.x - Beta versions with basic drift detection capabilities

---

## 📄 License

This project is licensed under the **Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License**.

**What this means:**
- ✅ **Free to use** for personal, educational, and internal business purposes
- ✅ **Free to modify** and distribute modifications under the same license
- ❌ **Cannot be sold** or used for commercial redistribution
- 📝 **Attribution required** - please credit the original project

**For businesses:** You can use this tool internally within your organization for drift detection without any licensing fees. You just cannot package and sell it as a commercial product.

See the [LICENSE](LICENSE) file for full details or visit [Creative Commons](https://creativecommons.org/licenses/by-nc-sa/4.0/) for more information.

---

**Built with ❤️ for the Azure DevOps community**