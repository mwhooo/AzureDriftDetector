# Azure Configuration Drift Detection Project

This C# console application compares Bicep/ARM template configurations with live Azure resources to detect configuration drift.

## Project Context
- **Purpose**: Detect configuration drift between expected (Bicep/ARM) and actual (Azure) resource states
- **Approach**: JSON comparison between template definitions and live Azure CLI query results
- **Language**: C# .NET console application
- **Integration**: Azure CLI for resource queries

## Development Guidelines
- Focus on JSON serialization/deserialization for Azure resource comparison
- Implement modular design for different Azure resource types
- Use Azure CLI process execution for live resource queries
- Provide clear drift reporting with specific property differences
- Support multiple resource types (Storage Accounts, Key Vaults, Virtual Networks, etc.)

## Key Components
- Azure CLI integration for live resource queries
- JSON comparison engine for drift detection
- Bicep/ARM template parsing
- Resource type-specific handlers
- Drift reporting and visualization

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

# Generate HTML report
dotnet run -- --bicep-file sample-template.bicep --resource-group yourResourceGroup --output Html
```

## Architecture
The application follows a clean, modular architecture:
- **Core**: Main orchestration logic
- **Services**: Azure CLI integration, Bicep conversion, comparison logic, reporting
- **Models**: Data structures for drift detection results
- **CLI**: Command-line interface with multiple output formats