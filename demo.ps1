# ============================================
# DEMO SCRIPT - Azure Configuration Drift Detector
# ============================================

# show the help
dotnet run -- --help

# show the RG in azure
start "https://portal.azure.com/#@atosbnndev.onmicrosoft.com/resource/subscriptions/83b81144-5906-45f5-87d5-805ad41e037c/resourcegroups/driftdetector-mark-rg/overview"

# show the bicep template including different types of resources
code samples/main-template.bicepparam

# ============================================
# STEP 1: Baseline - No Drift
# ============================================
# run the tool against environment (should show no drift initially)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg

# Compare with native Azure what-if (shows noise)
az deployment group what-if `
  --resource-group driftdetector-mark-rg `
  --template-file samples/main-template.bicep `
  --parameters samples/main-template.bicepparam
  #NOTE: --exclude-change-types -x  is not very useful sincs it will supress all deletes. modifies, or whatever you specify to supress
# ============================================
# INTRODUCE DRIFT - Choose scenario(s) to demo
# ============================================

# --- SCENARIO 1: Storage Account Drift (Simple & Clear) ---
# Change access tier from Hot to Cool
az storage account update `
  --name drifttestsabdd5hffg `
  --resource-group driftdetector-mark-rg `
  --access-tier Cool --yes

# Change to allow blob public access
az storage account update `
  --name drifttestsabdd5hffg `
  --resource-group driftdetector-mark-rg `
  --allow-blob-public-access true

# Change minimum TLS version
az storage account update `
  --name drifttestsabdd5hffg `
  --resource-group driftdetector-mark-rg `
  --min-tls-version TLS1_0


# --- SCENARIO 2: NSG Rule Drift (Complex Nested) ---
# Add an unauthorized SSH rule
az network nsg rule create `
  --resource-group driftdetector-mark-rg `
  --nsg-name drifttest-nsg `
  --name AllowSSH `
  --priority 200 `
  --access Allow `
  --direction Inbound `
  --protocol Tcp `
  --source-address-prefix '*' `
  --source-port-range '*' `
  --destination-address-prefix '*' `
  --destination-port-range 22

# Modify existing HTTP rule priority
az network nsg rule update `
  --resource-group driftdetector-mark-rg `
  --nsg-name drifttest-nsg `
  --name AllowHTTP `
  --priority 150

# --- SCENARIO 4: VNet Configuration Drift ---
# Add a new subnet not in template
az network vnet subnet create `
  --resource-group driftdetector-mark-rg `
  --vnet-name drifttest-vnet `
  --name unauthorized-subnet `
  --address-prefix 10.0.99.0/24


# --- SCENARIO 5: App Service Plan Drift ---
# Upgrade from Free to Basic tier (cost impact!)
az appservice plan update `
  --name drifttest-asp `
  --resource-group driftdetector-mark-rg `
  --sku B1


# --- SCENARIO 7: Tag Drift (Governance) ---
# Add unauthorized tag
az storage account update `
  --name drifttestsabdd5hffg `
  --resource-group driftdetector-mark-rg `
  --tags Environment=test Application=drifttest ResourceType=Infrastructure Owner=UnknownDepartment

# ============================================
# STEP 3: Detect Drift with Multiple Formats
# ============================================
# Console output (default)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg

# JSON output (for automation/CI-CD/AI)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg --output Json

# HTML report (for stakeholders/compliance)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg --output Html
# Then open the generated HTML file
explorer .

# ============================================
# STEP 4: Auto-Remediate Drift
# ============================================
# run with autofix to repair all drift automatically
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg --autofix

# ============================================
# STEP 5: Verify Drift is Fixed
# ============================================
# check again to show no drift
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg

dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg --output html

explorer .
# ============================================
# STEP 6: Demonstrate Intelligent Noise Filtering
# ============================================
# Temporarily rename drift-ignore config to show Azure platform noise
Rename-Item -Path "drift-ignore.json" -NewName "drift-ignore.json.bak" -ErrorAction SilentlyContinue

# Run without ignore config - will show lots of Azure platform noise (especially from AVM modules, servicebus, etc.)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg

# az will show the same changed
az deployment group what-if `
  --resource-group driftdetector-mark-rg `
  --template-file samples/main-template.bicep `
  --parameters samples/main-template.bicepparam

# Restore the ignore config
Rename-Item -Path "drift-ignore.json.bak" -NewName "drift-ignore.json" -ErrorAction SilentlyContinue

# Show the drift-ignore file and how it works
code drift-ignore.json
dotnet run -- -h

# Run with ignore config enabled (clean output)
dotnet run -- --bicep-file samples/main-template.bicepparam --resource-group driftdetector-mark-rg
