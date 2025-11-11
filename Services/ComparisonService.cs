using AzureDriftDetector.Models;
using Newtonsoft.Json.Linq;
using JsonDiffPatchDotNet;

namespace AzureDriftDetector.Services;

public class ComparisonService
{
    private readonly JsonDiffPatch _jsonDiffPatch;

    public ComparisonService()
    {
        _jsonDiffPatch = new JsonDiffPatch();
    }

    public DriftDetectionResult CompareResources(JObject expectedTemplate, List<AzureResource> liveResources)
    {
        var result = new DriftDetectionResult();
        var bicepService = new BicepService();
        var expectedResources = bicepService.ExtractResourcesFromTemplate(expectedTemplate);

        Console.WriteLine($"📋 Comparing {expectedResources.Count} expected resources with {liveResources.Count} live resources");

        foreach (var expectedResource in expectedResources)
        {
            var resourceType = expectedResource["type"]?.ToString() ?? "";
            var resourceName = expectedResource["name"]?.ToString() ?? "";
            bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";

            Console.WriteLine($"{(simpleOutput ? "[CHECK]" : "🔍")} Checking {resourceType}/{resourceName}");

            // Find matching live resource by type (since names might be parameterized in ARM templates)
            var matchingLiveResources = liveResources.Where(lr => 
                lr.Type.Equals(resourceType, StringComparison.OrdinalIgnoreCase)).ToList();

            AzureResource? liveResource = null;

            if (matchingLiveResources.Count == 1)
            {
                // If there's only one resource of this type, assume it's a match
                liveResource = matchingLiveResources.First();
                Console.WriteLine($"{(simpleOutput ? "[OK]" : "✅")} Found matching resource: {liveResource.Name}");
            }
            else if (matchingLiveResources.Count > 1)
            {
                // Try to match by name first
                liveResource = matchingLiveResources.FirstOrDefault(lr => 
                    lr.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));
                
                if (liveResource == null)
                {
                    Console.WriteLine($"{(simpleOutput ? "[WARN]" : "⚠️")}  Multiple resources of type {resourceType} found, using first one: {matchingLiveResources.First().Name}");
                    liveResource = matchingLiveResources.First();
                }
            }

            if (liveResource == null)
            {
                Console.WriteLine($"{(simpleOutput ? "[MISSING]" : "❌")} Resource not found in Azure: {resourceType}/{resourceName}");
                result.ResourceDrifts.Add(new ResourceDrift
                {
                    ResourceType = resourceType,
                    ResourceName = resourceName,
                    PropertyDrifts = new List<PropertyDrift>
                    {
                        new PropertyDrift
                        {
                            PropertyPath = "resource",
                            ExpectedValue = "exists",
                            ActualValue = "missing",
                            Type = DriftType.Missing
                        }
                    }
                });
                continue;
            }

            // Compare resource properties
            var resourceDrift = CompareResourceProperties(expectedResource, liveResource);
            if (resourceDrift.HasDrift)
            {
                Console.WriteLine($"{(simpleOutput ? "[DRIFT]" : "⚠️")}  Drift detected in {resourceType}/{resourceName}");
                result.ResourceDrifts.Add(resourceDrift);
            }
            else
            {
                Console.WriteLine($"{(simpleOutput ? "[OK]" : "✅")} No drift detected in {resourceType}/{resourceName}");
            }
        }

        result.HasDrift = result.ResourceDrifts.Any();
        result.Summary = GenerateSummary(result);

        return result;
    }

    private ResourceDrift CompareResourceProperties(JObject expectedResource, AzureResource liveResource)
    {
        var drift = new ResourceDrift
        {
            ResourceType = liveResource.Type,
            ResourceName = liveResource.Name,
            ResourceId = liveResource.Id
        };

        // Compare properties section
        if (expectedResource["properties"] is JObject expectedProps)
        {
            foreach (var expectedProp in expectedProps)
            {
                var propertyPath = $"properties.{expectedProp.Key}";
                var expectedValue = ParseJToken(expectedProp.Value);
                
                // Skip ARM template expressions
                if (expectedValue is string expectedStr && IsArmExpression(expectedStr))
                {
                    continue;
                }
                
                if (liveResource.Properties.TryGetValue(propertyPath, out var actualValue))
                {
                    if (!AreEqual(expectedValue, actualValue))
                    {
                        drift.PropertyDrifts.Add(new PropertyDrift
                        {
                            PropertyPath = propertyPath,
                            ExpectedValue = expectedValue,
                            ActualValue = actualValue,
                            Type = DriftType.Modified
                        });
                    }
                }
                else
                {
                    // Only report missing if it's not an ARM expression
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = propertyPath,
                        ExpectedValue = expectedValue,
                        ActualValue = null,
                        Type = DriftType.Missing
                    });
                }
            }
        }

        // Compare other important properties
        CompareProperty("location", expectedResource["location"], liveResource.Properties.GetValueOrDefault("location"), drift);
        CompareProperty("sku", expectedResource["sku"], liveResource.Properties.GetValueOrDefault("sku"), drift);
        CompareProperty("tags", expectedResource["tags"], liveResource.Properties.GetValueOrDefault("tags"), drift);

        // Check resource-specific settings that might have been changed
        CheckResourceSpecificSettings(liveResource, drift);

        // Remove any false positive drifts caused by ARM expressions
        drift.PropertyDrifts = drift.PropertyDrifts.Where(pd => !IsFalsePositiveArmExpression(pd)).ToList();

        return drift;
    }

    private void CompareProperty(string propertyName, JToken? expectedToken, object? actualValue, ResourceDrift drift)
    {
        var expectedValue = ParseJToken(expectedToken);
        
        // Skip comparison if expected value is an ARM template expression
        if (expectedValue is string expectedStr && IsArmExpression(expectedStr))
        {
            // Skip ARM template expressions like [parameters('location')], [subscription().tenantId], etc.
            return;
        }
        
        if (expectedValue != null && !AreEqual(expectedValue, actualValue))
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = propertyName,
                ExpectedValue = expectedValue,
                ActualValue = actualValue,
                Type = DriftType.Modified
            });
        }
    }

    private object? ParseJToken(JToken? token)
    {
        return token switch
        {
            null => null,
            JValue value => value.Value,
            JObject obj => obj.ToObject<Dictionary<string, object?>>(),
            JArray array => array.ToObject<List<object?>>(),
            _ => token.ToString()
        };
    }

    private bool AreEqual(object? expected, object? actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        // Special handling for SKU objects - only compare the 'name' field
        if (expected is Dictionary<string, object?> expectedDict && 
            actual is Dictionary<string, object?> actualDict &&
            expectedDict.ContainsKey("name") && actualDict.ContainsKey("name"))
        {
            return expectedDict["name"]?.ToString()?.Equals(actualDict["name"]?.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
        }

        // Use JSON serialization for deep comparison
        var expectedJson = Newtonsoft.Json.JsonConvert.SerializeObject(expected);
        var actualJson = Newtonsoft.Json.JsonConvert.SerializeObject(actual);
        
        return expectedJson.Equals(actualJson, StringComparison.OrdinalIgnoreCase);
    }

    private bool CompareSubnetLists(List<object?> expected, List<object?> actual)
    {
        // For subnet comparison, we want to match subnets by name and compare only core properties
        foreach (var expectedSubnet in expected)
        {
            // Handle both Dictionary and JObject types
            Dictionary<string, object?>? expectedDict = null;
            if (expectedSubnet is Dictionary<string, object?> dict)
            {
                expectedDict = dict;
            }
            else if (expectedSubnet is Newtonsoft.Json.Linq.JObject jobj)
            {
                expectedDict = jobj.ToObject<Dictionary<string, object?>>();
            }
            
            if (expectedDict != null && expectedDict.TryGetValue("name", out var expectedName))
            {
                var expectedNameStr = expectedName?.ToString();
                
                // Skip ARM expressions in subnet names
                if (expectedNameStr != null && IsArmExpression(expectedNameStr))
                {
                    // For ARM expressions, try to match by position or find the closest match
                    if (expected.Count == actual.Count && expected.Count == 1)
                    {
                        // Simple case: one expected, one actual subnet
                        return CompareSubnetProperties(expectedDict, actual.FirstOrDefault());
                    }
                    continue; // Skip ARM expression names for now
                }

                // Find matching subnet in actual by name
                var matchingActual = actual.FirstOrDefault(a =>
                {
                    if (a is Dictionary<string, object?> actualDict &&
                        actualDict.TryGetValue("name", out var actualName))
                    {
                        return actualName?.ToString()?.Equals(expectedNameStr, StringComparison.OrdinalIgnoreCase) ?? false;
                    }
                    return false;
                });

                if (matchingActual == null)
                {
                    return false; // Expected subnet not found
                }

                if (!CompareSubnetProperties(expectedDict, matchingActual))
                {
                    return false; // Subnet properties don't match
                }
            }
        }

        return true; // All expected subnets match
    }

    private bool CompareSubnetProperties(Dictionary<string, object?> expected, object? actual)
    {
        Dictionary<string, object?>? actualDict = null;
        if (actual is Dictionary<string, object?> dict)
        {
            actualDict = dict;
        }
        else if (actual is Newtonsoft.Json.Linq.JObject jobj)
        {
            actualDict = jobj.ToObject<Dictionary<string, object?>>();
        }
        
        if (actualDict == null)
        {
            return false;
        }

        // Compare core subnet properties that matter
        var importantProperties = new[] { "name", "properties" };

        foreach (var prop in importantProperties)
        {
            if (!expected.TryGetValue(prop, out var expectedValue))
                continue;

            if (!actualDict.TryGetValue(prop, out var actualValue))
            {
                return false;
            }

            // For properties sub-object, do a focused comparison
            if (prop == "properties")
            {
                // Handle various type combinations for properties
                Dictionary<string, object?>? expectedProps = null;
                Dictionary<string, object?>? actualProps = null;

                // Convert expected to Dictionary
                if (expectedValue is Dictionary<string, object?> expDict)
                {
                    expectedProps = expDict;
                }
                else if (expectedValue is JObject expJObj)
                {
                    expectedProps = expJObj.ToObject<Dictionary<string, object?>>();
                }

                // Convert actual to Dictionary  
                if (actualValue is Dictionary<string, object?> actDict)
                {
                    actualProps = actDict;
                }
                else if (actualValue is JObject actJObj)
                {
                    actualProps = actJObj.ToObject<Dictionary<string, object?>>();
                }

                if (expectedProps != null && actualProps != null)
                {
                    // First, compare properties that exist in the template
                    foreach (var expectedProp in expectedProps)
                    {
                        if (!actualProps.TryGetValue(expectedProp.Key, out var actualPropValue))
                        {
                            return false; // Expected property missing
                        }

                        if (!AreEqual(expectedProp.Value, actualPropValue))
                        {
                            return false; // Property values don't match
                        }
                    }
                    
                    // Second, check for important properties that were added manually in Azure
                    // These indicate manual configuration that deviates from IaC
                    var importantSubnetProperties = new[] { "serviceEndpoints", "networkSecurityGroup", "routeTable" };
                    
                    foreach (var importantProp in importantSubnetProperties)
                    {
                        // If this important property exists in Azure but not in template, that's drift
                        if (actualProps.ContainsKey(importantProp) && !expectedProps.ContainsKey(importantProp))
                        {
                            var actualPropValue = actualProps[importantProp];
                            
                            // Check if the property has meaningful content (not empty/null)
                            if (HasMeaningfulContent(actualPropValue))
                            {
                                return false; // Manual configuration detected - this is drift
                            }
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                // For name property, skip comparison if expected value is an ARM expression
                if (prop == "name" && expectedValue?.ToString() != null && IsArmExpression(expectedValue.ToString()))
                {
                    continue;
                }
                
                if (!AreEqual(expectedValue, actualValue))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasMeaningfulContent(object? value)
    {
        if (value == null) return false;
        
        // Handle JArray (service endpoints, delegations, etc.)
        if (value is JArray jArray)
        {
            return jArray.Count > 0;
        }
        
        // Handle List<object> 
        if (value is List<object?> list)
        {
            return list.Count > 0;
        }
        
        // Handle JObject (network security group, route table references)
        if (value is JObject jObj)
        {
            return jObj.Properties().Any();
        }
        
        // Handle Dictionary
        if (value is Dictionary<string, object?> dict)
        {
            return dict.Count > 0;
        }
        
        // Handle string values
        if (value is string str)
        {
            return !string.IsNullOrWhiteSpace(str);
        }
        
        // For other types, assume meaningful if not null
        return true;
    }

    private void CheckResourceSpecificSettings(AzureResource liveResource, ResourceDrift drift)
    {
        switch (liveResource.Type.ToLowerInvariant())
        {
            case "microsoft.storage/storageaccounts":
                CheckStorageAccountSettings(liveResource, drift);
                break;
            case "microsoft.keyvault/vaults":
                CheckKeyVaultSettings(liveResource, drift);
                break;
            case "microsoft.compute/virtualmachines":
                CheckVirtualMachineSettings(liveResource, drift);
                break;
            case "microsoft.network/virtualnetworks":
                CheckVirtualNetworkSettings(liveResource, drift);
                break;
            case "microsoft.sql/servers":
                CheckSqlServerSettings(liveResource, drift);
                break;
            // Add more resource types as needed
        }
    }

    private void CheckStorageAccountSettings(AzureResource liveResource, ResourceDrift drift)
    {
        // Check if public network access is disabled (common security change)
        // NOTE: Only flag this if it's not explicitly configured in the template
        // The regular property comparison will handle template-defined values
        // This is for detecting manual changes when not specified in template

        // Check network ACLs (this contains the firewall rules)
        if (liveResource.Properties.TryGetValue("properties.networkAcls", out var networkAcls) &&
            networkAcls is Dictionary<string, object?> networkDict &&
            networkDict.TryGetValue("defaultAction", out var defaultAction) &&
            defaultAction?.ToString() == "Deny")
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = "properties.networkAcls.defaultAction",
                ExpectedValue = "Allow (default)",
                ActualValue = "Deny",
                Type = DriftType.Modified
            });
        }
    }

    private void CheckKeyVaultSettings(AzureResource liveResource, ResourceDrift drift)
    {
        // Check for Key Vault network access configuration changes
        if (liveResource.Properties.TryGetValue("properties.networkAcls", out var networkAclsValue) &&
            networkAclsValue is Dictionary<string, object?> networkAcls)
        {
            // Check if default action is Deny (indicating firewall is enabled)
            if (networkAcls.TryGetValue("defaultAction", out var defaultActionValue) &&
                defaultActionValue?.ToString() == "Deny")
            {
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "properties.networkAcls.defaultAction",
                    ExpectedValue = "Allow (default/not configured)",
                    ActualValue = "Deny (selected networks only)",
                    Type = DriftType.Modified
                });
            }

            // Check for IP rules
            if (networkAcls.TryGetValue("ipRules", out var ipRulesValue))
            {
                if (ipRulesValue is List<object?> ipRules && ipRules.Count > 0)
                {
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = "properties.networkAcls.ipRules",
                        ExpectedValue = "not configured",
                        ActualValue = $"{ipRules.Count} IP rule(s) configured",
                        Type = DriftType.Added
                    });
                }
                else if (ipRulesValue is JArray ipRulesArray && ipRulesArray.Count > 0)
                {
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = "properties.networkAcls.ipRules",
                        ExpectedValue = "not configured",
                        ActualValue = $"{ipRulesArray.Count} IP rule(s) configured",
                        Type = DriftType.Added
                    });
                }
            }

            // Check for virtual network rules
            if (networkAcls.TryGetValue("virtualNetworkRules", out var vnetRulesValue))
            {
                if (vnetRulesValue is List<object?> vnetRules && vnetRules.Count > 0)
                {
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = "properties.networkAcls.virtualNetworkRules",
                        ExpectedValue = "not configured", 
                        ActualValue = $"{vnetRules.Count} virtual network rule(s) configured",
                        Type = DriftType.Added
                    });
                }
                else if (vnetRulesValue is JArray vnetRulesArray && vnetRulesArray.Count > 0)
                {
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = "properties.networkAcls.virtualNetworkRules",
                        ExpectedValue = "not configured",
                        ActualValue = $"{vnetRulesArray.Count} virtual network rule(s) configured",
                        Type = DriftType.Added
                    });
                }
            }
        }

        // Check for private endpoint connections
        if (liveResource.Properties.TryGetValue("properties.privateEndpointConnections", out var privateEndpointsValue) &&
            privateEndpointsValue is List<object?> privateEndpoints && privateEndpoints.Count > 0)
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = "properties.privateEndpointConnections",
                ExpectedValue = "not configured",
                ActualValue = $"{privateEndpoints.Count} private endpoint(s) configured",
                Type = DriftType.Added
            });
        }

        // Check for public network access changes
        // NOTE: Only flag this if it's not explicitly configured in the template
        // The regular property comparison will handle template-defined values
        // This is for detecting manual changes when not specified in template
    }

    private void CheckVirtualMachineSettings(AzureResource liveResource, ResourceDrift drift)
    {
        // Check for VM configuration changes like size, extensions, etc.
        // Add VM specific checks here
    }

    private void CheckVirtualNetworkSettings(AzureResource liveResource, ResourceDrift drift)
    {
        // Check for VNet configuration changes like address spaces, subnets, etc.
        if (liveResource.Properties.TryGetValue("properties.subnets", out var subnetsValue) &&
            subnetsValue is List<object?> liveSubnets)
        {
            // For virtual networks, we want to detect manually added subnets
            // that are not defined in the Bicep template
            CheckForUnexpectedSubnets(liveSubnets, drift);
            
            // Also check if any subnets have been modified
            CheckForModifiedSubnets(liveSubnets, drift);
        }

        // Check for changes in address space
        if (liveResource.Properties.TryGetValue("properties.addressSpace", out var addressSpaceValue) &&
            addressSpaceValue is Dictionary<string, object?> addressSpace &&
            addressSpace.TryGetValue("addressPrefixes", out var prefixesValue) &&
            prefixesValue is List<object?> addressPrefixes)
        {
            CheckForAddressSpaceChanges(addressPrefixes, drift);
        }
    }

    private void CheckSqlServerSettings(AzureResource liveResource, ResourceDrift drift)
    {
        // Check for SQL Server configuration changes like firewall rules, etc.
        // Add SQL Server specific checks here
    }

    private bool IsArmExpression(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        
        // ARM template expressions are wrapped in square brackets and contain functions
        return value.StartsWith("[") && value.EndsWith("]") && 
               (value.Contains("parameters(") || 
                value.Contains("subscription(") || 
                value.Contains("resourceGroup(") || 
                value.Contains("uniqueString(") || 
                value.Contains("format(") || 
                value.Contains("variables(") ||
                value.Contains("concat(") ||
                value.Contains("reference(") ||
                value.Contains("resourceId("));
    }

    private bool IsFalsePositiveArmExpression(PropertyDrift drift)
    {
        // Check if this is a false positive where we're comparing an ARM expression to its evaluated value
        if (drift.ExpectedValue is string expectedStr && IsArmExpression(expectedStr))
        {
            // These are common false positives to filter out
            return drift.PropertyPath == "location" || 
                   drift.PropertyPath == "properties.tenantId" ||
                   drift.PropertyPath.Contains("uniqueString") ||
                   (drift.PropertyPath == "sku" && drift.ExpectedValue?.ToString()?.Contains("Standard_LRS") == true);
        }

        // Check for tag expressions that resolve to actual values
        if (drift.PropertyPath == "tags" && drift.ExpectedValue != null && drift.ActualValue != null)
        {
            return IsTagDriftFalsePositive(drift);
        }

        // Check for complex object differences that are just Azure enrichment
        if (drift.PropertyPath.Contains("subnets") || drift.PropertyPath.Contains("networkAcls"))
        {
            return IsStructuralDriftFalsePositive(drift);
        }

        return false;
    }

    private bool IsTagDriftFalsePositive(PropertyDrift drift)
    {
        // Compare tags by resolving ARM expressions
        if (drift.ExpectedValue is Dictionary<string, object?> expectedTags &&
            drift.ActualValue is Dictionary<string, object?> actualTags)
        {
            // If the count and resolved values match, it's not real drift
            if (expectedTags.Count == actualTags.Count)
            {
                foreach (var expectedTag in expectedTags)
                {
                    var expectedValue = expectedTag.Value?.ToString();
                    if (IsArmExpression(expectedValue))
                    {
                        // Skip ARM expressions in tag comparison - this is a known limitation
                        continue;
                    }

                    if (!actualTags.ContainsKey(expectedTag.Key) || 
                        actualTags[expectedTag.Key]?.ToString() != expectedValue)
                    {
                        return false; // Real drift found
                    }
                }
                return true; // All tags match (ignoring ARM expressions)
            }
        }
        return false;
    }

    private bool IsStructuralDriftFalsePositive(PropertyDrift drift)
    {
        // For complex objects like subnets and networkAcls, check if core properties match
        if (drift.PropertyPath.Contains("subnets"))
        {
            // Don't filter out manually detected subnet additions/modifications
            if (drift.Type == DriftType.Added)
            {
                return false; // Keep genuine subnet additions
            }
            
            // For subnet property changes, check if it's a meaningful change
            // Keep changes to the subnets array itself (contains address prefix changes)
            if (drift.PropertyPath == "properties.subnets")
            {
                // Do smart subnet comparison instead of blanket "keep all subnet changes"
                if (drift.ExpectedValue is List<object?> expectedSubnets &&
                    drift.ActualValue is List<object?> actualSubnets)
                {
                    // If smart comparison shows they match, this is a false positive
                    return CompareSubnetLists(expectedSubnets, actualSubnets);
                }
                return false; // Keep subnet array changes if we can't do smart comparison
            }
            
            // Keep changes to important individual subnet properties 
            if (drift.PropertyPath.Contains("addressPrefix") || 
                drift.PropertyPath.Contains("networkSecurityGroup") ||
                drift.PropertyPath.Contains("routeTable"))
            {
                return false; // Keep important subnet property changes
            }
            
            // Only filter out structural differences from template comparisons
            // Subnets often have Azure-generated properties like etag, id, etc.
            return true; // Skip subnet structure differences for template comparisons
        }

        if (drift.PropertyPath.Contains("networkAcls"))
        {
            // Network ACLs get populated with empty arrays by Azure
            if (drift.ExpectedValue is Dictionary<string, object?> expectedAcls &&
                drift.ActualValue is Dictionary<string, object?> actualAcls)
            {
                // Check if core properties match
                var expectedAction = expectedAcls.GetValueOrDefault("defaultAction")?.ToString();
                var actualAction = actualAcls.GetValueOrDefault("defaultAction")?.ToString();
                var expectedBypass = expectedAcls.GetValueOrDefault("bypass")?.ToString();
                var actualBypass = actualAcls.GetValueOrDefault("bypass")?.ToString();

                return expectedAction == actualAction && expectedBypass == actualBypass;
            }
        }

        return false;
    }

    private void CheckForUnexpectedSubnets(List<object?> liveSubnets, ResourceDrift drift)
    {
        // Common subnet names that indicate manual additions
        var manuallyAddedSubnetNames = new[] { "default", "GatewaySubnet", "AzureFirewallSubnet", "AzureBastionSubnet" };
        
        foreach (var subnet in liveSubnets)
        {
            // Handle both JObject (from JSON parsing) and Dictionary types
            Dictionary<string, object?>? subnetDict = null;
            if (subnet is JObject jSubnet)
            {
                subnetDict = jSubnet.ToObject<Dictionary<string, object?>>();
            }
            else if (subnet is Dictionary<string, object?> dict)
            {
                subnetDict = dict;
            }
            
            if (subnetDict != null &&
                subnetDict.TryGetValue("name", out var nameValue) &&
                nameValue is string subnetName)
            {
                // Check if this looks like a manually added subnet
                if (manuallyAddedSubnetNames.Contains(subnetName, StringComparer.OrdinalIgnoreCase))
                {
                    // Get address prefix from properties
                    var addressPrefix = ExtractSubnetAddressPrefix(subnetDict);

                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = $"properties.subnets['{subnetName}']",
                        ExpectedValue = "not defined",
                        ActualValue = $"subnet with address prefix {addressPrefix}",
                        Type = DriftType.Added
                    });
                }
                
                // Also check for any subnet that doesn't match the template pattern
                // This is a simple heuristic - could be made more sophisticated
                if (!subnetName.EndsWith("-subnet") && !manuallyAddedSubnetNames.Contains(subnetName, StringComparer.OrdinalIgnoreCase))
                {
                    // Get address prefix from properties  
                    var addressPrefix = ExtractSubnetAddressPrefix(subnetDict);

                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = $"properties.subnets['{subnetName}']",
                        ExpectedValue = "not defined in template",
                        ActualValue = $"manually added subnet with address prefix {addressPrefix}",
                        Type = DriftType.Added
                    });
                }
            }
        }
    }

    private void CheckForModifiedSubnets(List<object?> liveSubnets, ResourceDrift drift)
    {
        // Check for modifications in existing subnets
        foreach (var subnet in liveSubnets)
        {
            if (subnet is Dictionary<string, object?> subnetDict &&
                subnetDict.TryGetValue("name", out var nameValue) &&
                nameValue is string subnetName)
            {
                // Check for common subnet modifications
                CheckSubnetSecurityChanges(subnetName, subnetDict, drift);
            }
        }
    }

    private void CheckSubnetSecurityChanges(string subnetName, Dictionary<string, object?> subnetDict, ResourceDrift drift)
    {
        // Check for network security group associations
        if (subnetDict.TryGetValue("networkSecurityGroup", out var nsgValue) && nsgValue != null)
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = $"properties.subnets['{subnetName}'].networkSecurityGroup",
                ExpectedValue = "not configured",
                ActualValue = "NSG associated",
                Type = DriftType.Added
            });
        }

        // Check for route table associations
        if (subnetDict.TryGetValue("routeTable", out var routeTableValue) && routeTableValue != null)
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = $"properties.subnets['{subnetName}'].routeTable",
                ExpectedValue = "not configured",
                ActualValue = "Route table associated",
                Type = DriftType.Added
            });
        }

        // Check for service endpoints
        if (subnetDict.TryGetValue("serviceEndpoints", out var serviceEndpointsValue) &&
            serviceEndpointsValue is List<object?> serviceEndpoints &&
            serviceEndpoints.Count > 0)
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = $"properties.subnets['{subnetName}'].serviceEndpoints",
                ExpectedValue = "not configured",
                ActualValue = $"{serviceEndpoints.Count} service endpoint(s) configured",
                Type = DriftType.Added
            });
        }
    }

    private void CheckForAddressSpaceChanges(List<object?> addressPrefixes, ResourceDrift drift)
    {
        // Check if additional address spaces have been added
        // This is a simple check - in a full implementation, you'd compare against the template
        if (addressPrefixes.Count > 1)
        {
            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = "properties.addressSpace.addressPrefixes",
                ExpectedValue = "single address space",
                ActualValue = $"{addressPrefixes.Count} address spaces configured",
                Type = DriftType.Modified
            });
        }
    }

    private string ExtractSubnetAddressPrefix(Dictionary<string, object?> subnetDict)
    {
        if (subnetDict.TryGetValue("properties", out var propsValue))
        {
            if (propsValue is Dictionary<string, object?> props &&
                props.TryGetValue("addressPrefix", out var prefixValue))
            {
                return prefixValue?.ToString() ?? "unknown";
            }
            else if (propsValue is JObject jProps && jProps["addressPrefix"] != null)
            {
                return jProps["addressPrefix"]?.ToString() ?? "unknown";
            }
        }
        return "unknown";
    }

    private string GenerateSummary(DriftDetectionResult result)
    {
        if (!result.HasDrift)
        {
            return "No configuration drift detected.";
        }

        var driftCount = result.ResourceDrifts.Count;
        var propertyDriftCount = result.ResourceDrifts.Sum(rd => rd.PropertyDrifts.Count);
        
        return $"Configuration drift detected in {driftCount} resource(s) with {propertyDriftCount} property difference(s).";
    }
}