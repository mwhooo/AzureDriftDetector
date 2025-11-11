# 🔍 Azure Configuration Drift Monitoring

This project includes automated Azure configuration drift monitoring using GitHub Actions.

## 🚀 Quick Setup

### 1. Azure Service Principal
Create a service principal for GitHub Actions:

```bash
# Create service principal
az ad sp create-for-rbac --name "DriftDetector-CI" --role "Reader" --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID" --sdk-auth

# Add additional permissions if needed
az role assignment create --assignee YOUR_SP_APP_ID --role "Reader" --scope "/subscriptions/YOUR_SUBSCRIPTION_ID"
```

### 2. GitHub Secrets
Add these secrets to your repository (Settings → Secrets and variables → Actions):

- `AZURE_CLIENT_ID` - Service principal application ID
- `AZURE_TENANT_ID` - Your Azure tenant ID  
- `AZURE_SUBSCRIPTION_ID` - Your Azure subscription ID

### 3. Configure Environments
Update `.github/workflows/drift-monitoring.yml` with your:
- Environment names
- Resource group names
- Bicep template files

## 📊 How It Works

### Automatic Monitoring
- **Schedule**: Runs every 6 hours automatically
- **Environments**: Monitors dev, staging, and prod
- **Detection**: Compares Bicep templates with live Azure resources
- **Reporting**: Creates GitHub issues when drift is detected

### Manual Monitoring
Trigger monitoring manually:
```bash
# Via GitHub CLI
gh workflow run drift-monitoring.yml

# Via GitHub web interface
# Go to Actions → Azure Drift Monitoring → Run workflow
```

### Drift Detection Process
1. **Download** latest drift detector binary
2. **Login** to Azure using service principal
3. **Compare** Bicep templates with live resources
4. **Report** findings via GitHub issues
5. **Upload** detailed reports as artifacts

## 📋 Monitoring Features

### ✅ When No Drift Detected
- Runs silently (unless manually triggered)
- Closes any existing drift issues
- Logs success in workflow summary

### 🚨 When Drift Detected
- Creates GitHub issue with details
- Uploads JSON and HTML reports
- Updates existing issues with new findings
- Sets workflow status to failed

### 📊 Reports and Artifacts
- **JSON reports**: Machine-readable drift details
- **HTML reports**: Human-friendly visualization
- **Workflow logs**: Detailed execution information
- **Issues**: Trackable drift alerts with remediation steps

## 🔧 Customization

### Environment Configuration
```yaml
strategy:
  matrix:
    environment: 
      - { name: 'dev', resource_group: 'my-dev-rg' }
      - { name: 'prod', resource_group: 'my-prod-rg' }
```

### Monitoring Schedule
```yaml
schedule:
  # Every 6 hours
  - cron: '0 */6 * * *'
  # Daily at 9 AM UTC
  - cron: '0 9 * * *'
  # Business hours only (Mon-Fri 9-17 UTC)
  - cron: '0 9-17 * * 1-5'
```

### Output Formats
The monitoring uses `--simple-output` for better CI compatibility, but you can also generate HTML reports:

```bash
./AzureDriftDetector --bicep-file template.bicep --resource-group myRG --output Html
```

## 🚨 Issue Management

### Automatic Issue Creation
- **Title**: "🚨 Drift Detected in ENVIRONMENT"
- **Labels**: `drift-alert`, `environment-name`
- **Content**: Detailed drift information and remediation steps

### Issue Lifecycle
1. **Creation**: New issue for first-time drift
2. **Updates**: Comments added for ongoing drift
3. **Resolution**: Issue closed when drift resolved
4. **Prevention**: Monitoring continues to prevent regression

## 📈 Best Practices

### For Production Environments
- Set up notification webhooks for critical drift
- Configure resource group locks where appropriate
- Implement approval workflows for template changes
- Use separate service principals per environment

### For Development Teams
- Review drift issues during daily standups
- Update templates when intentional changes are made
- Use pull requests to track infrastructure changes
- Document any temporary drift exceptions

## 🔍 Troubleshooting

### Common Issues
1. **Permission Errors**: Ensure service principal has Reader access
2. **Resource Not Found**: Check resource group names and regions
3. **Template Errors**: Validate Bicep syntax and parameters
4. **Network Issues**: Verify Azure connectivity and firewall rules

### Debugging
```bash
# Test locally with same settings
./AzureDriftDetector --bicep-file template.bicep --resource-group myRG --simple-output

# Check Azure CLI access
az account show
az group list
az resource list --resource-group myRG
```

### Workflow Debugging
- Check workflow logs in Actions tab
- Verify secrets are properly configured
- Test Azure login step separately
- Validate Bicep template syntax

## 🚀 Advanced Features

### Integration with Other Tools
- **Slack/Teams**: Add webhook notifications
- **ServiceNow**: Create incidents for critical drift
- **Email**: Send reports to infrastructure teams
- **PagerDuty**: Alert on-call engineers

### Custom Reporting
```bash
# Generate multiple report formats
./AzureDriftDetector --output Json --simple-output
./AzureDriftDetector --output Html
./AzureDriftDetector --output Markdown
```

### Conditional Monitoring
```yaml
# Only monitor on main branch
if: github.ref == 'refs/heads/main'

# Skip weekends
if: github.event.schedule != '0 */6 * * 0,6'
```

## 📊 Example Workflow Output

### Successful Run (No Drift)
```
✅ Monitor dev - No drift detected
✅ Monitor staging - No drift detected  
✅ Monitor prod - No drift detected
```

### Drift Detected
```
❌ Monitor dev - Drift detected in 2 resources
   - Storage account: encryption settings changed
   - Virtual network: subnet added manually
   
📊 Issue #123 created: "🚨 Drift Detected in DEV"
📤 Reports uploaded as artifacts
```

This monitoring system helps maintain infrastructure compliance and catches configuration drift before it becomes a problem! 🎯