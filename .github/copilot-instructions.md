# Azure Configuration Drift Detection Project

This C# console application compares Bicep/ARM template configurations with live Azure resources to detect configuration drift and automatically remediate it.

## Project Context
- **Purpose**: Detect and automatically fix configuration drift between expected (Bicep/ARM) and actual (Azure) resource states
- **Approach**: JSON comparison between template definitions and live Azure CLI query results, with optional automatic deployment and intelligent ignore filtering
- **Language**: C# .NET console application
- **Integration**: Azure CLI for resource queries and template deployments
- **Noise Reduction**: Configurable ignore patterns to suppress Azure platform behaviors and false positives

## Development Guidelines
- Focus on JSON serialization/deserialization for Azure resource comparison
- Implement modular design for different Azure resource types
- Use Azure CLI process execution for live resource queries AND template deployments
- Provide clear drift reporting with specific property differences
- Support multiple resource types (Storage Accounts, Key Vaults, Virtual Networks, etc.)
- Enable safe automatic drift remediation via Bicep template deployment
- Implement comprehensive ignore patterns to filter Azure platform noise and behaviors outside user control
- Design ignore system to distinguish between legitimate drift and Azure platform modifications

## Key Components
- Azure CLI integration for live resource queries and deployments
- JSON comparison engine for drift detection with ignore filtering
- Bicep/ARM template parsing and deployment
- Resource type-specific handlers
- Drift reporting and visualization
- Automatic remediation with --autofix flag
- Configurable drift ignore system for suppressing Azure platform behaviors
- Pattern matching engine for flexible ignore rule application

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

# Use custom ignore configuration to suppress Azure platform noise
dotnet run -- --bicep-file sample-template.bicep --resource-group yourResourceGroup --ignore-config custom-ignore.json
```

## Architecture
The application follows a clean, modular architecture:
- **Core**: Main orchestration logic
- **Services**: Azure CLI integration, Bicep conversion, comparison logic, reporting
- **Models**: Data structures for drift detection results
- **CLI**: Command-line interface with multiple output formats