# Test External Module Analysis

## Structure Analysis
The external module `br:pebiceptemplates.azurecr.io/bicep/storage/storageaccount:v1.1.0` gets compiled into a complex nested structure:

1. **Main Module Deployment**: `Microsoft.Resources/deployments` - `storageModule`
   - Contains inline template with the actual storage account resources
   - Has nested sub-deployments for various storage services

2. **Actual Resources Created**:
   - `Microsoft.Storage/storageAccounts` - The main storage account
   - Various nested deployments for blob services, file services, queue services, table services
   - Conditional resources based on parameters

## Problem
The drift detector is seeing the outer deployment wrapper instead of drilling down to find the actual `Microsoft.Storage/storageAccounts` resource that should be compared.

## Solution Needed
Enhance the resource extraction to properly traverse deeply nested module deployments and extract the real infrastructure resources (not just deployment wrappers).