# Documentation

This directory contains comprehensive documentation for the Azure Configuration Drift Detector.

## Available Documentation

### [Drift Ignore Configuration Guide](DRIFT-IGNORE.md)
**Essential reading for production use!** Comprehensive guide to configuring the drift ignore system to suppress Azure platform behaviors and false positive drift detections.

**Topics covered:**
- Configuration file structure and syntax
- Resource-specific ignore rules with conditional matching
- Global pattern matching for common Azure properties
- Examples for major Azure services (Storage, Service Bus, Key Vault, etc.)
- Best practices and troubleshooting
- Advanced features like pattern matching and multiple conditions

### [Security Setup Guide](SECURITY-SETUP.md) 
Configuration guide for GitHub Advanced Security features including CodeQL analysis and dependency scanning.

### [Bicep Module Development Guide](BICEP-BUILD.md)
Documentation for developing and maintaining the modular Bicep templates used by the drift detector.

### [Release Notes](RELEASE-NOTES.md)
Version history and changelog for the Azure Configuration Drift Detector.

### [Monitoring Setup](../MONITORING.md)
Guide for setting up automated drift monitoring with GitHub Actions.

### [OpenID Connect Setup](../OIDC-SETUP.md)
Instructions for configuring OIDC authentication with Azure for secure GitHub Actions workflows.

## Quick Links

- **Getting Started**: See main [README.md](../README.md)
- **Drift Ignore Examples**: Jump to [Common Patterns](DRIFT-IGNORE.md#common-ignore-patterns)
- **Troubleshooting**: See [Drift Ignore Troubleshooting](DRIFT-IGNORE.md#troubleshooting)
- **GitHub Actions**: See [Monitoring Setup](../MONITORING.md)

## Contributing

When adding new documentation:
1. Follow the existing structure and formatting
2. Include practical examples
3. Add cross-references to related documentation
4. Update this index file with new documents