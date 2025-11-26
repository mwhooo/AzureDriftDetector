# Azure Configuration Drift Detector - Demo Guide

**Version**: 3.6.0  
**Demo Duration**: 15-20 minutes  
**Audience**: Technical teams, DevOps engineers, Cloud architects

---

## 🎯 Demo Objectives

Show how the tool:
1. Detects configuration drift between Bicep templates and live Azure resources
2. Provides clear, actionable drift reports in multiple formats
3. Automatically remediates drift with `--autofix`
4. Intelligently filters Azure platform noise with ignore patterns
5. Handles complex scenarios (NSGs, Key Vaults, Storage Accounts, VNets)

---

## 📋 Prerequisites Checklist

Before the demo:
- [ ] Azure CLI installed and logged in (`az login`)
- [ ] Bicep CLI installed (`az bicep version`)
- [ ] .NET 8.0 SDK installed
- [ ] Sample resources deployed to Azure
- [ ] Terminal with good font size for screen sharing
- [ ] Build the tool: `dotnet build`

---

## 🎬 Demo Script

### **Part 1: Introduction & Setup** (2 minutes)

#### What is Configuration Drift?
> "Configuration drift occurs when the actual state of your Azure resources differs from what's defined in your Infrastructure as Code templates. This can happen due to manual changes, automation errors, or Azure platform updates."

#### The Problem
- Manual Azure portal changes that bypass IaC
- Compliance violations that go undetected
- Infrastructure that diverges from documented state
- Time-consuming manual audits

#### Our Solution
Show the tool's main capabilities:
```bash
dotnet run -- --help
```

**Key points to highlight:**
- Bicep/ARM template comparison with live Azure
- Multiple output formats (Console, JSON, HTML)
- Automatic drift remediation
- Intelligent noise suppression

---

### **Part 2: Basic Drift Detection** (3 minutes)

#### Scenario: Storage Account with Manual Changes

**Setup Story:**
> "Let's say a developer manually changed a storage account's access tier from Hot to Cool in the Azure portal to save costs during development."

**1. Show the Bicep template:**
```bash
code sample-template.bicep
```

Point out the expected configuration:
```bicep
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'  // ← This is in template
  }
}
```

**2. Run drift detection:**
```bash
dotnet run -- --bicep-file sample-template.bicep --resource-group rg-demo-driftdetector
```

**What to highlight in the output:**
- ✅ Resources processed count
- ⚠️ Drift detected warning
- 📊 Clear property-level differences
- 🎯 Shows `accessTier: Cool` (Azure) vs `Hot` (template)

**Expected output example:**
```
🔍 Drift Detection Results
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Resource: storageAccount
Type: Microsoft.Storage/storageAccounts
Status: ⚠️ DRIFT DETECTED

Properties with drift:
  • properties.accessTier
    Template: "Hot"
    Azure:    "Cool"
```

---

### **Part 3: HTML Report Generation** (2 minutes)

#### Show Professional Reporting

**1. Generate HTML report:**
```bash
dotnet run -- --bicep-file sample-template.bicep --resource-group rg-demo-driftdetector --output Html
```

**2. Open the generated report:**
```bash
# The tool outputs the file path
start drift-report-*.html
```

**What to showcase in the HTML report:**
- 📊 Executive summary with statistics
- 🎨 Color-coded drift severity
- 🔍 Expandable property details
- 📋 Copy-friendly JSON diffs
- 🖨️ Print-ready formatting
- 📅 Timestamp and metadata

**Demo talking point:**
> "This HTML report is perfect for compliance audits, stakeholder reviews, or documentation. It's self-contained and can be archived or shared via email."

---

### **Part 4: Intelligent Noise Suppression** (3 minutes)

#### Scenario: Azure Platform Behaviors

**Setup Story:**
> "Azure automatically adds system-managed properties that we don't define in templates. The tool intelligently filters this 'noise' to show only meaningful drift."

**1. Show what gets filtered:**
```bash
dotnet run -- --bicep-file complex-template.bicep --resource-group rg-demo-driftdetector --verbose
```

**Key examples of ignored noise:**
- `provisioningState` - Azure runtime state
- `creationTime` / `lastModified` - Timestamps
- System-assigned identities
- Auto-generated keys
- Platform-managed tags

**2. Show custom ignore configuration:**
```bash
code drift-ignore-config.json
```

Example ignore patterns:
```json
{
  "globalIgnores": [
    "**.provisioningState",
    "**.creationTime"
  ],
  "resourceTypeIgnores": {
    "Microsoft.Storage/storageAccounts": [
      "properties.primaryEndpoints.**",
      "properties.networkAcls.resourceAccessRules[*].resourceId"
    ]
  }
}
```

**Demo talking point:**
> "Without intelligent filtering, you'd see hundreds of false positives. Our ignore system focuses your attention on drift that actually matters - the configuration you control."

---

### **Part 5: Automatic Drift Remediation** (4 minutes)

#### The Power Feature: `--autofix`

**Setup Story:**
> "When drift is detected, you typically need to manually re-deploy your template. The --autofix flag automates this entire workflow."

**1. Detect drift first (show the problem):**
```bash
dotnet run -- --bicep-file sample-template.bicep --resource-group rg-demo-driftdetector
```

**2. Auto-remediate the drift:**
```bash
dotnet run -- --bicep-file sample-template.bicep --resource-group rg-demo-driftdetector --autofix
```

**What happens under the hood (explain):**
1. ✅ Detects drift
2. 🔄 Converts Bicep to ARM JSON
3. 🚀 Deploys template to Azure
4. ⏱️ Waits for deployment completion
5. ✅ Verifies drift is resolved

**Expected output:**
```
⚠️ Drift detected in 1 resource(s)

🔧 Auto-fix enabled. Deploying template to remediate drift...

Deployment started: drift-fix-20251126-143022
⏳ Waiting for deployment to complete...
✅ Deployment succeeded!

✅ Drift remediation completed successfully
```

**3. Verify drift is gone:**
```bash
dotnet run -- --bicep-file sample-template.bicep --resource-group rg-demo-driftdetector
```

**Expected output:**
```
✅ No drift detected! All resources match the template.
```

**Demo talking point:**
> "This is GitOps in action - your template is the source of truth, and the tool ensures Azure stays in sync automatically."

---

### **Part 6: Complex Scenario - Network Security Groups** (3 minutes)

#### Scenario: NSG Rules Drift

**Setup Story:**
> "Network Security Groups are complex with nested rules. Let's see how the tool handles detailed rule-level drift."

**1. Show the template NSG configuration:**
```bash
code complex-template.bicep
```

Point out NSG rules section.

**2. Manually add a rule in Azure portal:**
- Open Azure portal
- Navigate to NSG
- Add a new inbound rule (e.g., "AllowSSH" on port 22)

**3. Detect the drift:**
```bash
dotnet run -- --bicep-file complex-template.bicep --resource-group rg-demo-driftdetector
```

**What to highlight:**
- 🎯 Detects the extra rule not in template
- 📋 Shows full rule configuration (ports, protocols, source/destination)
- 🔍 Clear "Extra" vs "Missing" vs "Modified" categorization
- ✅ Other rules that match are not flagged

**4. Show JSON output for detailed analysis:**
```bash
dotnet run -- --bicep-file complex-template.bicep --resource-group rg-demo-driftdetector --output Json > drift.json
code drift.json
```

**Demo talking point:**
> "For complex resources like NSGs, the detailed JSON output helps you understand exactly what changed, making troubleshooting much easier."

---

### **Part 7: CI/CD Integration** (2 minutes)

#### Show Pipeline Integration

**1. Show the GitHub Actions workflow:**
```bash
code .github/workflows/drift-detection.yml
```

**Key points to highlight:**
```yaml
- name: Detect Configuration Drift
  run: |
    dotnet run -- \
      --bicep-file main.bicep \
      --resource-group ${{ vars.RESOURCE_GROUP }} \
      --output Json
```

**2. Show example pipeline results:**
- Navigate to GitHub Actions
- Show successful drift detection run
- Show failed run with drift (blocking deployment)

**Demo talking point:**
> "You can integrate this into your CI/CD pipeline to:
> - Block deployments if drift is detected
> - Generate drift reports as artifacts
> - Auto-remediate drift on schedule
> - Alert teams via Slack/Teams on drift detection"

**3. Show scheduled drift monitoring:**
```yaml
on:
  schedule:
    - cron: '0 */6 * * *'  # Every 6 hours
```

---

### **Part 8: Real-World Use Cases** (2 minutes)

#### When to Use This Tool

**1. Compliance Auditing**
> "Prove that production matches approved templates for SOC2, ISO27001, or internal compliance."

**2. Drift Prevention in Production**
> "Run scheduled checks to catch manual changes before they cause incidents."

**3. Multi-Environment Consistency**
> "Verify dev, staging, and prod are all configured identically per your templates."

**4. Post-Incident Recovery**
> "After an incident where manual changes were made, quickly restore to template state."

**5. Infrastructure Handoff**
> "When inheriting infrastructure, understand what actually exists vs what's documented."

---

## 🎓 Q&A Preparation

### Common Questions

**Q: Does this work with Terraform?**
A: Currently Bicep/ARM only, but Terraform support is on the roadmap.

**Q: What about secrets and sensitive data?**
A: The tool doesn't access Key Vault secrets - it compares configuration, not content.

**Q: Can I customize what's ignored?**
A: Yes! Use `drift-ignore-config.json` with glob patterns for full control.

**Q: How long does a scan take?**
A: Typically 10-30 seconds depending on resource count. Primarily limited by Azure CLI query time.

**Q: Does --autofix work in read-only subscriptions?**
A: No, you need deployment permissions. The tool will fail gracefully with a clear error.

**Q: Can I run this locally in VS Code?**
A: Absolutely! That's the primary development workflow. Just ensure Azure CLI is logged in.

---

## 🔧 Demo Environment Setup

### Quick Setup Script

```bash
# 1. Create resource group
az group create --name rg-demo-driftdetector --location eastus

# 2. Deploy sample template
az deployment group create \
  --resource-group rg-demo-driftdetector \
  --template-file sample-template.bicep \
  --parameters storageAccountName=stdriftdemo$(date +%s)

# 3. Manually create drift (optional)
# Change storage account access tier to Cool in portal
# Or via CLI:
az storage account update \
  --name stdriftdemo123 \
  --resource-group rg-demo-driftdetector \
  --access-tier Cool
```

### Cleanup Script

```bash
# Delete demo resources
az group delete --name rg-demo-driftdetector --yes --no-wait
```

---

## 📊 Demo Metrics to Showcase

Throughout the demo, emphasize these achievements:

- ✅ **Detection Accuracy**: Finds all meaningful drift, filters noise
- ⚡ **Speed**: Scans dozens of resources in seconds
- 🎯 **Precision**: Property-level granularity
- 🔧 **Automation**: One-command remediation
- 📈 **Scalability**: Works with complex multi-resource templates
- 🛡️ **Safety**: What-if preview before auto-fix
- 📄 **Reporting**: Multiple output formats for different audiences

---

## 🎤 Closing Statements

**Key Takeaways:**
1. Configuration drift is a real problem in cloud environments
2. Manual audits are time-consuming and error-prone
3. This tool automates detection and remediation
4. Integrates seamlessly into CI/CD pipelines
5. Saves time, reduces risk, improves compliance

**Call to Action:**
> "The tool is open source on GitHub. Try it with your templates, open issues if you find bugs, and contribute if you have ideas for improvements!"

**Next Steps:**
- Share GitHub repo link
- Provide documentation links
- Schedule follow-up for questions
- Offer help with initial setup

---

## 📝 Demo Notes

### Tips for Success

1. **Practice the flow** - Run through once before the actual demo
2. **Have backup slides** - In case Azure CLI is slow
3. **Use large font size** - Everyone should see clearly
4. **Explain as you type** - Verbalize what commands do
5. **Show failures too** - Demonstrate error handling
6. **Time management** - Keep to 15-20 minutes, save time for Q&A

### Backup Plan

If live demo fails:
- Have screenshots/recordings ready
- Show HTML report examples
- Walk through code structure
- Focus on pipeline integration examples

### Terminal Setup

```bash
# Increase font size for screen sharing
# Set theme to high contrast
# Clear terminal: clear
# Set prompt to show only current directory
```

---

## 🔗 Resources to Share

- **GitHub Repository**: https://github.com/mwhooo/AzureDriftDetector
- **Latest Release**: v3.6.0
- **Documentation**: README.md
- **Security Setup**: docs/SECURITY-SETUP.md
- **OIDC Guide**: OIDC-SETUP.md
- **Release Notes**: docs/RELEASE-NOTES.md

---

**Good luck with your demo! 🚀**
