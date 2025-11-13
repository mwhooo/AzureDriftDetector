# Azure Configuration Drift Detection Project

This C# console application compares Bicep/ARM template configurations with live Azure resources to detect configuration drift and automatically remediate it.

## Project Context
- **Purpose**: Detect and automatically fix configuration drift between expected (Bicep/ARM) and actual (Azure) resource states
- **Approach**: JSON comparison between template definitions and live Azure CLI query results, with optional automatic deployment
- **Language**: C# .NET console application
- **Integration**: Azure CLI for resource queries and template deployments

## Development Guidelines
- Focus on JSON serialization/deserialization for Azure resource comparison
- Implement modular design for different Azure resource types
- Use Azure CLI process execution for live resource queries AND template deployments
- Provide clear drift reporting with specific property differences
- Support multiple resource types (Storage Accounts, Key Vaults, Virtual Networks, etc.)
- Enable safe automatic drift remediation via Bicep template deployment

## Key Components
- Azure CLI integration for live resource queries and deployments
- JSON comparison engine for drift detection
- Bicep/ARM template parsing and deployment
- Resource type-specific handlers
- Drift reporting and visualization
- Automatic remediation with --autofix flag

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Azure CLI (logged in)
- Bicep CLI

### Build and Run
```bash
# Build the project
dotnet build

# Run with sample template
dotnet run -- --bicep-file sample-template.bicep --resource-group yourResourceGroup

# Run with automatic drift remediation
dotnet run -- --bicep-file sample-template.bicep --resource-group yourResourceGroup --autofix

# Generate HTML report
dotnet run -- --bicep-file sample-template.bicep --resource-group yourResourceGroup --output Html
```

## Architecture
The application follows a clean, modular architecture:
- **Core**: Main orchestration logic
- **Services**: Azure CLI integration, Bicep conversion, comparison logic, reporting
- **Models**: Data structures for drift detection results
- **CLI**: Command-line interface with multiple output formats