# Bicep Build Instructions

This document explains how to build Bicep templates to ARM JSON when needed.

## Building Individual Modules

To compile individual Bicep modules to ARM JSON:

```bash
# Build storage account module
bicep build bicep-modules/storage-account.bicep

# Build virtual network module  
bicep build bicep-modules/virtual-network.bicep
```

## Building Main Template

To compile the main complex template:

```bash
# Build main template
bicep build complex-template.bicep
```

## Building All Templates

To build all Bicep files at once:

```powershell
# PowerShell - build all .bicep files recursively
Get-ChildItem -Recurse -Filter "*.bicep" | ForEach-Object { bicep build $_.FullName }
```

```bash
# Bash - build all .bicep files recursively  
find . -name "*.bicep" -exec bicep build {} \;
```

## Note on Generated Files

The generated JSON files are:
- ✅ **Build artifacts** - generated from source Bicep files
- ❌ **Not tracked in git** - excluded via .gitignore  
- 🔄 **Generated as needed** - by the drift detector tool or manual build

## Automatic Building

The Azure Drift Detector automatically compiles Bicep to ARM JSON as part of its process, so manual building is typically not required unless you want to inspect the generated ARM templates.