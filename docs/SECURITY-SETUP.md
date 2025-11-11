# Security Setup Guide

This guide explains how to enable GitHub's security features for the Azure Drift Detector project.

## 🛡️ GitHub Advanced Security for Private Repositories

### Prerequisites
- GitHub Pro, Team, or Enterprise account
- Repository admin access
- Private repository (public repos have these features enabled by default)

### Step-by-Step Setup

#### 1. Enable GitHub Advanced Security
1. Navigate to your repository on GitHub
2. Go to **Settings** → **Security & analysis**
3. Under **GitHub Advanced Security**, click **Enable**
4. Confirm the billing impact (if applicable)

#### 2. Enable Code Scanning (CodeQL)
1. In **Security & analysis**, find **Code scanning**
2. Click **Set up** → **CodeQL Analysis**
3. Choose **Default** setup for automatic configuration
4. Click **Enable CodeQL**

Alternatively, use our existing workflow (already configured):
- The `.github/workflows/codeql-analysis.yml` workflow will start working once Advanced Security is enabled

#### 3. Enable Additional Security Features
1. **Dependency graph**: Usually enabled by default
2. **Dependabot alerts**: Enable to get notified about vulnerable dependencies
3. **Dependabot security updates**: Auto-create PRs for security fixes
4. **Secret scanning**: Detects accidentally committed secrets
5. **Push protection**: Prevents pushing secrets to the repository

### ⚡ Quick Enable via GitHub CLI

If you have admin access, you can enable features programmatically:

```powershell
# Enable GitHub Advanced Security (requires appropriate billing plan)
gh api --method PATCH repos/:owner/:repo \
  --field security_and_analysis='{"advanced_security":{"status":"enabled"}}'

# Enable secret scanning
gh api --method PATCH repos/:owner/:repo \
  --field security_and_analysis='{"secret_scanning":{"status":"enabled"}}'

# Enable secret scanning push protection
gh api --method PATCH repos/:owner/:repo \
  --field security_and_analysis='{"secret_scanning_push_protection":{"status":"enabled"}}'
```

**Note**: The exact API calls may vary depending on your GitHub plan and repository settings.

## 🔍 What Each Feature Does

### CodeQL Analysis
- **Purpose**: Static analysis for security vulnerabilities
- **Languages**: C#, JavaScript, Python, Java, C++, Go, Ruby
- **Frequency**: On every push and pull request
- **Output**: Security alerts in the Security tab

### Dependency Scanning
- **Purpose**: Monitors dependencies for known vulnerabilities
- **Source**: GitHub Advisory Database, WhiteSource, and others
- **Alerts**: Automatic notifications for vulnerable packages
- **Auto-fixes**: Dependabot can create PRs with security updates

### Secret Scanning
- **Purpose**: Prevents accidental commit of secrets (API keys, passwords, tokens)
- **Coverage**: 200+ secret patterns from major service providers
- **Push Protection**: Blocks pushes containing secrets
- **Historical Scanning**: Scans entire repository history

## 🚨 Troubleshooting

### "Code scanning is not enabled" Error
**Problem**: CodeQL workflow fails with "Code scanning is not enabled"
**Solution**: Enable GitHub Advanced Security in repository settings

### "Invalid security_and_analysis payload" Error
**Problem**: API calls fail when trying to enable features
**Solution**: Verify you have admin access and the correct GitHub plan

### Features Not Visible in Settings
**Problem**: Security features don't appear in repository settings
**Solution**: 
- Verify you have a GitHub Pro/Team/Enterprise account
- Ensure you have admin access to the repository
- Contact GitHub support if features should be available but aren't visible

## 💰 Billing Considerations

### GitHub Plans
- **Public repositories**: All security features are free
- **Private repositories**: Require GitHub Advanced Security
  - **GitHub Pro**: $4/month per user
  - **GitHub Team**: $4/month per user (included)
  - **GitHub Enterprise**: Contact GitHub for pricing

### Cost Calculation
GitHub Advanced Security for private repos is typically charged per:
- **Active committer**: Users who have committed in the last 90 days
- **Repository**: Some features may have per-repository charges

## ✅ Verification

After enabling security features, verify they're working:

1. **CodeQL**: Check that workflows complete successfully in Actions tab
2. **Dependency graph**: View dependencies in Insights → Dependency graph  
3. **Security alerts**: Check Security tab for any detected issues
4. **Secret scanning**: Test by attempting to commit a dummy API key (don't actually push it!)

## 📚 Additional Resources

- [GitHub Advanced Security Documentation](https://docs.github.com/en/github/getting-started-with-github/about-github-advanced-security)
- [CodeQL Documentation](https://docs.github.com/en/code-security/secure-coding/automatically-scanning-your-code-for-vulnerabilities-and-errors/about-code-scanning)
- [Secret Scanning Documentation](https://docs.github.com/en/code-security/secret-scanning/about-secret-scanning)
- [Dependabot Documentation](https://docs.github.com/en/code-security/supply-chain-security/managing-vulnerabilities-in-your-projects-dependencies/about-dependabot-security-updates)

---

🛡️ **Security is a shared responsibility** - these tools help catch issues early, but secure coding practices are still essential!