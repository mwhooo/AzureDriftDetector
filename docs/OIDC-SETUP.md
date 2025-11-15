# 🔐 OIDC Authentication Setup for Azure Drift Monitoring

This guide shows how to set up **secretless authentication** between GitHub Actions and Azure using OpenID Connect (OIDC) federated credentials.

## 🎯 Why OIDC?

- **🔒 No secrets stored** in GitHub repository
- **🔄 Automatic token rotation** and expiration
- **🛡️ Enhanced security** with short-lived tokens
- **📋 Auditable** authentication events
- **🎛️ Fine-grained permissions** based on GitHub context

## 🚀 Setup Steps

### 1. Create Azure Service Principal

```bash
# Create service principal with Reader role
az ad sp create-for-rbac --name "DriftDetector-CI" \
  --role "Reader" \
  --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID"

# Note: Don't use --sdk-auth flag for OIDC setup
```

### 2. Configure Federated Credentials

Create federated credentials for different GitHub contexts:

#### Main Branch Access
```bash
az ad app federated-credential create --id YOUR_CLIENT_ID --parameters '{
  "name": "DriftDetector-GitHub-Actions-Main",
  "issuer": "https://token.actions.githubusercontent.com", 
  "subject": "repo:YOUR_ORG/YOUR_REPO:ref:refs/heads/main",
  "description": "GitHub Actions federated credential for main branch",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

#### Pull Request Access  
```bash
az ad app federated-credential create --id YOUR_CLIENT_ID --parameters '{
  "name": "DriftDetector-GitHub-Actions-PR",
  "issuer": "https://token.actions.githubusercontent.com", 
  "subject": "repo:YOUR_ORG/YOUR_REPO:pull_request",
  "description": "GitHub Actions federated credential for pull requests",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

#### Environment-Specific (Optional)
```bash
# For production environment only
az ad app federated-credential create --id YOUR_CLIENT_ID --parameters '{
  "name": "DriftDetector-GitHub-Actions-Prod",
  "issuer": "https://token.actions.githubusercontent.com", 
  "subject": "repo:YOUR_ORG/YOUR_REPO:environment:production",
  "description": "GitHub Actions federated credential for production environment",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

### 3. Set GitHub Repository Secrets

Use GitHub CLI to set **secrets** (these contain sensitive information):

```bash
# Set repository secrets for sensitive Azure identifiers
gh secret set AZURE_CLIENT_ID --body "YOUR_CLIENT_ID"
gh secret set AZURE_TENANT_ID --body "YOUR_TENANT_ID" 
gh secret set AZURE_SUBSCRIPTION_ID --body "YOUR_SUBSCRIPTION_ID"
```

Or set them via GitHub web interface:
- Go to **Settings** → **Secrets and variables** → **Actions** → **Secrets** tab
- Add the three secrets above

### 4. Update Workflow Configuration

The workflow needs these permissions:

```yaml
permissions:
  contents: read
  issues: write  
  id-token: write  # Required for OIDC authentication
```

And this authentication configuration:

```yaml
- name: Azure Login
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

## 🔍 Verification

### Test Authentication
```bash
# Trigger workflow manually to test OIDC
gh workflow run drift-monitoring.yml

# Check workflow status  
gh run list --workflow="drift-monitoring.yml" --limit 1

# View workflow logs
gh run view --log
```

### Verify Federated Credentials
```bash
# List configured federated credentials
az ad app federated-credential list --id YOUR_CLIENT_ID
```

### Check Azure Login
In the workflow logs, you should see:
```
✅ Azure Login successful
✅ Token acquired via OIDC federation  
✅ No client secret required
```

## 🛡️ Security Benefits

### Federated Identity Claims

OIDC tokens include GitHub-specific claims:
- **Repository**: `repo:owner/name`
- **Branch**: `ref:refs/heads/main`
- **Pull Request**: `pull_request`
- **Environment**: `environment:prod`
- **Actor**: `actor:username`
- **Workflow**: `workflow:workflow-name`

### Azure Token Validation

Azure validates these claims before issuing access tokens:
- ✅ **Issuer verification**: Must be `token.actions.githubusercontent.com`
- ✅ **Subject matching**: Must match configured subject pattern
- ✅ **Audience validation**: Must be `api://AzureADTokenExchange`
- ✅ **Expiration check**: Tokens expire in 15 minutes

## 🔧 Advanced Configuration

### Environment-Based Access

Create separate federated credentials per environment:

```yaml
# Production-only access
subject: "repo:owner/name:environment:production"

# Staging-only access  
subject: "repo:owner/name:environment:staging"

# Branch-specific access
subject: "repo:owner/name:ref:refs/heads/release/*"
```

### Multiple Repository Access

For organization-wide service principals:

```bash
# Allow access from multiple repositories
subject: "repo:my-org/*:ref:refs/heads/main"

# Or specific repositories
subject: "repo:my-org/infrastructure:ref:refs/heads/main"
subject: "repo:my-org/monitoring:ref:refs/heads/main"
```

### Conditional Access Policies

Combine with Azure Conditional Access:
- **IP restrictions**: Only from GitHub's IP ranges
- **Device compliance**: Require compliant devices
- **Risk-based access**: Block suspicious sign-ins

## 🚨 Troubleshooting

### Common Issues

1. **Authentication Failed**
   ```
   Error: AADSTS70021: No matching federated identity record found
   ```
   - Check subject pattern matches exactly
   - Verify issuer is `https://token.actions.githubusercontent.com`

2. **Permission Denied**
   ```
   Error: The client does not have authorization to perform action
   ```
   - Ensure service principal has required Azure RBAC roles
   - Check subscription-level permissions

3. **Token Exchange Failed**
   ```
   Error: AADSTS50105: The signed in user is not assigned to a role
   ```
   - Verify federated credential configuration
   - Check audience is `api://AzureADTokenExchange`

### Debug Commands

```bash
# Check GitHub token (in Actions runner)
echo $ACTIONS_ID_TOKEN_REQUEST_TOKEN

# Verify service principal permissions
az role assignment list --assignee YOUR_CLIENT_ID

# Test Azure CLI access
az account show
az group list
```

## 📊 Monitoring and Auditing

### Azure AD Audit Logs
Monitor authentication events:
- Sign-in logs for federated identity usage
- Application audit logs for credential management
- Risk detection events

### GitHub Audit Logs  
Track workflow authentication:
- Workflow run authentication events
- OIDC token issuance
- Failed authentication attempts

This OIDC setup provides **enterprise-grade security** without the overhead of managing secrets! 🎯