# 📋 Sample Bicep Templates

This directory contains example Bicep templates and parameter files for testing and demonstrating the Azure Drift Detector tool.

## 📁 Contents

### Main Template
- **`main-template.bicep`** - Main infrastructure template that demonstrates common Azure resources
- **`main-template.bicepparam`** - Parameter file for the main template with sample values
- **`main-template.json`** - Compiled ARM JSON template (auto-generated)

## 🚀 Usage

These templates are used for:

### Testing Drift Detection
```bash
# Run drift detection against the sample template
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group your-rg

# Test with auto-fix
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group your-rg --autofix
```

### CI/CD Pipeline Testing
The GitHub Actions workflows use these templates to validate the drift detection functionality across different scenarios.

### Local Development
Use these templates to test new features and verify drift detection logic before deploying to production environments.

## 🔧 Customization

1. **Copy the template files** to create your own test scenarios
2. **Modify parameters** in the `.bicepparam` file to match your environment
3. **Add new resources** to test additional drift detection capabilities

## 📚 Related Documentation

- [Bicep Module Library](../bicep-modules/README.md) - Reusable Bicep modules
- [Drift Ignore Configuration](../docs/DRIFT-IGNORE.md) - Configure drift detection rules
- [Project Documentation](../docs/README.md) - Full project documentation

## ⚠️ Important Notes

- These are **sample templates** for testing purposes
- **Do not use in production** without proper review and customization
- Some resources may incur Azure costs when deployed
- Always clean up test resources after use