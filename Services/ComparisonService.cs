using AzureDriftDetector.Models;
using Newtonsoft.Json.Linq;
using JsonDiffPatchDotNet;

namespace AzureDriftDetector.Services;

public class ComparisonService
{
    private readonly JsonDiffPatch _jsonDiffPatch;
    private readonly DriftIgnoreService _ignoreService;

    public ComparisonService(DriftIgnoreService? ignoreService = null)
    {
        _jsonDiffPatch = new JsonDiffPatch();
        _ignoreService = ignoreService ?? new DriftIgnoreService();
    }

    public DriftDetectionResult CompareResources(JObject expectedTemplate, List<AzureResource> liveResources)
    {
        var result = new DriftDetectionResult();
        
        // Check if we should use what-if results directly
        if (expectedTemplate["_useWhatIfResults"]?.Value<bool>() == true)
        {
            Console.WriteLine($"🔄 Analyzing what-if results for configuration drift...");
            var rawResult = ParseWhatIfResults(expectedTemplate);
            return _ignoreService.FilterIgnoredDrifts(rawResult);
        }
        
        // Fall back to manual comparison if what-if not used
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
        
        // Special handling for tags - resolve ARM expressions before comparison
        if (propertyName == "tags" && expectedToken != null)
        {
            CompareTagsWithArmResolution(expectedToken, actualValue, drift);
            return;
        }
        
        // Skip comparison if expected value is an ARM template expression (for non-tag properties)
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

    private void CompareTagsWithArmResolution(JToken expectedToken, object? actualValue, ResourceDrift drift)
    {
        try
        {
            // Parse expected tags (may contain ARM expressions)
            var expectedTags = ParseJToken(expectedToken) as Dictionary<string, object?>;
            var actualTags = actualValue as Dictionary<string, object?>;

            if (expectedTags == null)
            {
                // If expected is null but actual has tags, that's drift
                if (actualTags != null && actualTags.Count > 0)
                {
                    drift.PropertyDrifts.Add(new PropertyDrift
                    {
                        PropertyPath = "tags",
                        ExpectedValue = null,
                        ActualValue = actualTags,
                        Type = DriftType.Missing
                    });
                }
                return;
            }

            // Resolve ARM expressions in expected tags
            var resolvedExpectedTags = new Dictionary<string, object?>();
            foreach (var tag in expectedTags)
            {
                var resolvedValue = ResolveArmExpressionForComparison(tag.Value?.ToString());
                resolvedExpectedTags[tag.Key] = resolvedValue;
            }

            // Compare resolved expected tags with actual tags
            if (!AreTagsEqual(resolvedExpectedTags, actualTags))
            {
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "tags",
                    ExpectedValue = resolvedExpectedTags,
                    ActualValue = actualTags,
                    Type = DriftType.Modified
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Error comparing tags: {ex.Message}");
        }
    }

    private string? ResolveArmExpressionForComparison(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsArmExpression(value))
        {
            return value;
        }

        // For comparison purposes, resolve common ARM expressions
        // This is a simplified resolution for demonstration - in production you'd want more comprehensive resolution
        if (value == "[parameters('environmentName')]")
        {
            return "test"; // Default value from template
        }
        else if (value == "[parameters('applicationName')]")
        {
            return "drifttest"; // Default value from template  
        }
        else if (value == "[parameters('location')]")
        {
            return "westeurope"; // Common default, though this could vary
        }

        // For unresolved expressions, return the expression itself
        // This allows comparison to show the difference
        return value;
    }

    private bool AreTagsEqual(Dictionary<string, object?>? expected, Dictionary<string, object?>? actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        // Check if all expected tags exist and match in actual
        foreach (var expectedTag in expected)
        {
            if (!actual.TryGetValue(expectedTag.Key, out var actualValue))
            {
                return false; // Expected tag missing in actual
            }

            var expectedVal = expectedTag.Value?.ToString();
            var actualVal = actualValue?.ToString();
            
            if (expectedVal != actualVal)
            {
                return false; // Tag values don't match
            }
        }

        // Check if actual has extra tags (optional - depends on drift policy)
        // For now, we'll allow extra tags and only flag missing/different expected tags
        // If you want to flag extra tags as drift, uncomment:
        // return expected.Count == actual.Count;

        return true;
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

        // Special handling for security rules arrays
        if (expected is List<object?> expectedList && actual is List<object?> actualList)
        {
            // Check if this is a security rules array by examining the content
            if (IsSecurityRulesArray(expectedList) || IsSecurityRulesArray(actualList))
            {
                return CompareSecurityRules(expectedList, actualList);
            }
            
            // Check if this is a subnets array
            if (IsSubnetsArray(expectedList) || IsSubnetsArray(actualList))
            {
                return CompareSubnetLists(expectedList, actualList);
            }
            
            // For other arrays, fall back to default comparison
        }

        // Special handling for Log Analytics workspace features
        if (expected is Dictionary<string, object?> expectedFeatures && 
            actual is Dictionary<string, object?> actualFeatures &&
            IsLogAnalyticsFeatures(expectedFeatures, actualFeatures))
        {
            return CompareLogAnalyticsFeatures(expectedFeatures, actualFeatures);
        }

        // Use JSON serialization for deep comparison
        var expectedJson = Newtonsoft.Json.JsonConvert.SerializeObject(expected);
        var actualJson = Newtonsoft.Json.JsonConvert.SerializeObject(actual);
        
        return expectedJson.Equals(actualJson, StringComparison.OrdinalIgnoreCase);
    }

    private bool CompareSecurityRules(List<object?> expected, List<object?> actual)
    {
        if (expected.Count != actual.Count) return false;

        // Compare security rules by matching core properties
        foreach (var expectedRule in expected)
        {
            if (!TryGetSecurityRuleProperties(expectedRule, out var expectedProps)) continue;
            
            var matchingActualRule = actual.FirstOrDefault(actualRule =>
            {
                if (!TryGetSecurityRuleProperties(actualRule, out var actualProps)) return false;
                
                // Match by name first
                return expectedProps.ContainsKey("name") && actualProps.ContainsKey("name") &&
                       expectedProps["name"]?.ToString()?.Equals(actualProps["name"]?.ToString(), StringComparison.OrdinalIgnoreCase) == true;
            });

            if (matchingActualRule == null) return false;

            if (!TryGetSecurityRuleProperties(matchingActualRule, out var matchingActualProps)) return false;

            // Compare core security rule properties
            var coreProperties = new[] { "access", "direction", "priority", "protocol", 
                                       "sourcePortRange", "destinationPortRange", 
                                       "sourceAddressPrefix", "destinationAddressPrefix" };

            foreach (var prop in coreProperties)
            {
                var expectedValue = GetSecurityRuleProperty(expectedProps, prop);
                var actualValue = GetSecurityRuleProperty(matchingActualProps, prop);

                if (!AreSecurityRuleValuesEqual(expectedValue, actualValue))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool TryGetSecurityRuleProperties(object? rule, out Dictionary<string, object?> properties)
    {
        properties = new Dictionary<string, object?>();

        if (rule is Dictionary<string, object?> ruleDict)
        {
            // Extract name from root level
            if (ruleDict.TryGetValue("name", out var name))
            {
                properties["name"] = name;
            }

            // Extract properties from properties sub-object
            if (ruleDict.TryGetValue("properties", out var props) && props is Dictionary<string, object?> propsDict)
            {
                foreach (var prop in propsDict)
                {
                    properties[prop.Key] = prop.Value;
                }
            }
            return true;
        }

        if (rule is JObject jRule)
        {
            properties = jRule.ToObject<Dictionary<string, object?>>() ?? new Dictionary<string, object?>();
            
            // Flatten properties sub-object
            if (properties.TryGetValue("properties", out var props) && props is JObject propsJObj)
            {
                var propsDict = propsJObj.ToObject<Dictionary<string, object?>>();
                if (propsDict != null)
                {
                    foreach (var prop in propsDict)
                    {
                        properties[prop.Key] = prop.Value;
                    }
                }
            }
            return true;
        }

        return false;
    }

    private object? GetSecurityRuleProperty(Dictionary<string, object?> ruleProps, string propertyName)
    {
        if (ruleProps.TryGetValue(propertyName, out var value))
        {
            return value;
        }

        // Also check in nested properties object
        if (ruleProps.TryGetValue("properties", out var properties) && 
            properties is Dictionary<string, object?> propsDict &&
            propsDict.TryGetValue(propertyName, out var nestedValue))
        {
            return nestedValue;
        }

        return null;
    }

    private bool AreSecurityRuleValuesEqual(object? expected, object? actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        // Convert both to strings for comparison (handles different numeric types)
        var expectedStr = expected.ToString();
        var actualStr = actual.ToString();

        return expectedStr?.Equals(actualStr, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private bool IsLogAnalyticsFeatures(Dictionary<string, object?> expected, Dictionary<string, object?> actual)
    {
        // Check if this looks like a Log Analytics workspace features comparison
        return expected.ContainsKey("enableLogAccessUsingOnlyResourcePermissions") ||
               actual.ContainsKey("enableLogAccessUsingOnlyResourcePermissions") ||
               actual.ContainsKey("legacy") ||
               actual.ContainsKey("searchVersion");
    }

    private bool CompareLogAnalyticsFeatures(Dictionary<string, object?> expected, Dictionary<string, object?> actual)
    {
        // For Log Analytics features, only compare the properties we explicitly set in the template
        // Ignore Azure-added defaults like legacy and searchVersion
        
        foreach (var expectedFeature in expected)
        {
            if (!actual.TryGetValue(expectedFeature.Key, out var actualValue))
            {
                return false; // Expected feature is missing
            }

            if (!AreEqual(expectedFeature.Value, actualValue))
            {
                return false; // Feature values don't match
            }
        }

        // Don't fail for additional Azure-added features in actual that aren't in expected
        return true;
    }

    private bool IsSecurityRulesArray(List<object?> list)
    {
        if (list.Count == 0) return false;
        
        // Check if the first item looks like a security rule
        var firstItem = list.First();
        if (firstItem is Dictionary<string, object?> dict)
        {
            return dict.ContainsKey("properties") && 
                   (dict.ContainsKey("access") || HasSecurityRuleProperties(dict));
        }
        
        if (firstItem is JObject jObj)
        {
            return jObj.ContainsKey("properties") &&
                   (jObj.ContainsKey("access") || HasSecurityRuleProperties(jObj));
        }
        
        return false;
    }

    private bool IsSubnetsArray(List<object?> list)
    {
        if (list.Count == 0) return false;
        
        // Check if the first item looks like a subnet
        var firstItem = list.First();
        if (firstItem is Dictionary<string, object?> dict)
        {
            return dict.ContainsKey("addressPrefix") || HasSubnetProperties(dict);
        }
        
        if (firstItem is JObject jObj)
        {
            return jObj.ContainsKey("addressPrefix") || HasSubnetProperties(jObj);
        }
        
        return false;
    }

    private bool HasSecurityRuleProperties(Dictionary<string, object?> dict)
    {
        if (dict.TryGetValue("properties", out var props) && props is Dictionary<string, object?> propsDict)
        {
            return propsDict.ContainsKey("access") || propsDict.ContainsKey("direction") || 
                   propsDict.ContainsKey("protocol") || propsDict.ContainsKey("priority");
        }
        return false;
    }

    private bool HasSecurityRuleProperties(JObject jObj)
    {
        var props = jObj["properties"] as JObject;
        if (props != null)
        {
            return props.ContainsKey("access") || props.ContainsKey("direction") || 
                   props.ContainsKey("protocol") || props.ContainsKey("priority");
        }
        return false;
    }

    private bool HasSubnetProperties(Dictionary<string, object?> dict)
    {
        if (dict.TryGetValue("properties", out var props) && props is Dictionary<string, object?> propsDict)
        {
            return propsDict.ContainsKey("addressPrefix") || propsDict.ContainsKey("privateEndpointNetworkPolicies");
        }
        return dict.ContainsKey("addressPrefix");
    }

    private bool HasSubnetProperties(JObject jObj)
    {
        var props = jObj["properties"] as JObject;
        if (props != null)
        {
            return props.ContainsKey("addressPrefix") || props.ContainsKey("privateEndpointNetworkPolicies");
        }
        return jObj.ContainsKey("addressPrefix");
    }

    private bool CompareSubnetLists(List<object?> expected, List<object?> actual)
    {
        // First check if there are extra subnets in actual that aren't in expected
        if (actual.Count > expected.Count)
        {
            return false; // Extra subnets detected
        }
        
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

    private DriftDetectionResult ParseWhatIfResults(JObject expectedTemplate)
    {
        var result = new DriftDetectionResult();
        var whatIfOutput = expectedTemplate["_whatIfOutput"]?.ToString() ?? "";
        
        if (string.IsNullOrWhiteSpace(whatIfOutput))
        {
            result.Summary = "No what-if output available";
            return result;
        }

        // Parse the what-if output to detect drift
        // What-if uses symbols: 
        // = (no change), ~ (modify), + (create), - (delete), x (no effect)
        
        var lines = whatIfOutput.Split('\n');
        ResourceDrift? currentResourceDrift = null;
        PropertyDrift? currentPropertyDrift = null;
        var complexObjectDetails = new List<string>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Check if this is a resource line (starts with a symbol at the beginning or after 2 spaces)
            var trimmedLine = line.TrimStart();
            if (trimmedLine.Length == 0) continue;
            
            var firstChar = trimmedLine[0];
            
            // Check if this is a resource definition line (contains Microsoft. and ends with version)
            if ((firstChar == '~' || firstChar == '+' || firstChar == '-' || firstChar == '=') && 
                trimmedLine.Contains("Microsoft.") && trimmedLine.Contains('['))
            {
                // Save previous property drift with details if exists
                if (currentPropertyDrift != null && complexObjectDetails.Count > 0)
                {
                    // Use formatted details instead of raw join
                    currentPropertyDrift.ActualValue = FormatComplexObjectDetails(complexObjectDetails, currentPropertyDrift.PropertyPath);
                    UpdateExpectedValueForComplexObject(currentPropertyDrift);
                    currentPropertyDrift = null;
                    complexObjectDetails.Clear();
                }
                
                // Save previous resource drift if exists
                if (currentResourceDrift != null && currentResourceDrift.PropertyDrifts.Count > 0)
                {
                    result.ResourceDrifts.Add(currentResourceDrift);
                }
                
                // Extract resource info
                var resourceInfo = ExtractResourceInfoFromWhatIfLine(trimmedLine);
                
                // Skip resources that couldn't be parsed properly
                if (resourceInfo.type == "skip" && resourceInfo.name == "skip")
                {
                    continue;
                }
                
                if (firstChar == '~')
                {
                    // Modified resource - create drift object to collect property changes
                    currentResourceDrift = new ResourceDrift
                    {
                        ResourceType = resourceInfo.type,
                        ResourceName = resourceInfo.name,
                        PropertyDrifts = new List<PropertyDrift>()
                    };
                }
                else if (firstChar == '+')
                {
                    // Resource will be created - this means the resource is missing in Azure
                    // This IS drift - report it as missing
                    result.ResourceDrifts.Add(new ResourceDrift
                    {
                        ResourceType = resourceInfo.type,
                        ResourceName = resourceInfo.name,
                        PropertyDrifts = new List<PropertyDrift>
                        {
                            new PropertyDrift
                            {
                                PropertyPath = "resource",
                                ExpectedValue = "defined in template",
                                ActualValue = "missing in Azure",
                                Type = DriftType.Missing
                            }
                        }
                    });
                    currentResourceDrift = null;
                }
                else if (firstChar == '-')
                {
                    // Extra resource (exists in Azure but not in template) - this IS drift
                    result.ResourceDrifts.Add(new ResourceDrift
                    {
                        ResourceType = resourceInfo.type,
                        ResourceName = resourceInfo.name,
                        PropertyDrifts = new List<PropertyDrift>
                        {
                            new PropertyDrift
                            {
                                PropertyPath = "resource",
                                ExpectedValue = "not defined in template",
                                ActualValue = "exists in Azure",
                                Type = DriftType.Added
                            }
                        }
                    });
                    currentResourceDrift = null;
                }
                else if (firstChar == '=')
                {
                    // No change - no drift
                    currentResourceDrift = null;
                }
            }
            else if (currentResourceDrift != null && line.StartsWith("    "))
            {
                // This is a property change line (indented under a modified resource)
                // Property lines are indented with 4 spaces
                var propertyLine = line.Substring(4); // Remove leading spaces
                
                if (propertyLine.Length > 0 && (propertyLine[0] == '~' || propertyLine[0] == '+' || propertyLine[0] == '-'))
                {
                    // Save previous property drift with details if exists
                    if (currentPropertyDrift != null && complexObjectDetails.Count > 0)
                    {
                        var formattedDetails = FormatComplexObjectDetails(complexObjectDetails, currentPropertyDrift.PropertyPath);
                        currentPropertyDrift.ActualValue = formattedDetails;
                        UpdateExpectedValueForComplexObject(currentPropertyDrift);
                        
                        currentPropertyDrift = null;
                        complexObjectDetails.Clear();
                    }
                    
                    var propertyDrift = ExtractPropertyDriftFromWhatIfLine(propertyLine);
                    if (propertyDrift != null)
                    {
                        currentResourceDrift.PropertyDrifts.Add(propertyDrift);
                        
                        // Check if this is a complex object/array that might have details in following lines
                        var actualValue = propertyDrift.ActualValue?.ToString() ?? "";
                        if (actualValue == "differs in Azure (complex object/array)" || 
                            actualValue == "configuration differs (details will be analyzed)")
                        {
                            currentPropertyDrift = propertyDrift;
                            complexObjectDetails.Clear();
                        }
                    }
                }
                else if (currentPropertyDrift != null)
                {
                    // This might be a detail line for the current complex property (more deeply indented)
                    // Collect these lines to show the actual changes
                    var detailLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(detailLine))
                    {
                        complexObjectDetails.Add(detailLine);
                    }
                }
            }
        }
        
        // Save any pending property drift with details
        if (currentPropertyDrift != null && complexObjectDetails.Count > 0)
        {
            // Format the details with a helpful summary
            var formattedDetails = FormatComplexObjectDetails(complexObjectDetails, currentPropertyDrift.PropertyPath);
            currentPropertyDrift.ActualValue = formattedDetails;
            UpdateExpectedValueForComplexObject(currentPropertyDrift);
        }
        
        // Add the last resource drift if still pending
        if (currentResourceDrift != null && currentResourceDrift.PropertyDrifts.Count > 0)
        {
            result.ResourceDrifts.Add(currentResourceDrift);
        }
        
        result.HasDrift = result.ResourceDrifts.Any();
        result.Summary = GenerateSummary(result);
        
        return result;
    }

    private (string type, string name) ExtractResourceInfoFromWhatIfLine(string line)
    {
        // Extract resource type and name from lines like:
        // "  ~ Microsoft.Storage/storageAccounts/mystorage [2023-05-01]"
        // "  + Microsoft.Storage/storageAccounts/newstorage [2023-05-01]"
        // "  + Microsoft.ServiceBus/namespaces/drifttest-servicebus/queues/deadletter [2022-10-01-preview]"
        
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var resourcePath = parts[1];
            
            // Skip malformed or invalid resource paths
            if (string.IsNullOrWhiteSpace(resourcePath) || !resourcePath.Contains('/'))
            {
                Console.WriteLine($"📝 Skipping malformed what-if line: {line.Trim()}");
                return ("skip", "skip"); // Use special marker to indicate skipping
            }
            
            var pathParts = resourcePath.Split('/');
            
            if (pathParts.Length >= 3)
            {
                // Handle child resources (e.g., queues under Service Bus namespace)
                // For child resources like: Microsoft.ServiceBus/namespaces/namespaceName/queues/queueName
                // We want to extract: Microsoft.ServiceBus/namespaces/queues and the full identifier
                if (pathParts.Length > 3)
                {
                    // This is a child resource
                    // Format: Provider/ParentType/ParentName/ChildType/ChildName[/...]
                    // We need: Provider/ParentType/ChildType as type, and ParentName/ChildName as identifier
                    var resourceType = $"{pathParts[0]}/{pathParts[1]}/{pathParts[3]}";
                    var resourceIdentifier = $"{pathParts[2]}/{pathParts[4]}";
                    return (resourceType, resourceIdentifier);
                }
                else
                {
                    // Regular resource
                    var resourceType = $"{pathParts[0]}/{pathParts[1]}";
                    var resourceName = pathParts[2];
                    
                    // Validate that we have meaningful values
                    if (string.IsNullOrWhiteSpace(resourceName))
                    {
                        Console.WriteLine($"📝 Skipping what-if line with empty resource name: {line.Trim()}");
                        return ("skip", "skip");
                    }
                    
                    return (resourceType, resourceName);
                }
            }
        }
        
        // Log the problematic line for debugging
        Console.WriteLine($"📝 Could not parse what-if line, skipping: {line.Trim()}");
        return ("skip", "skip"); // Use special marker instead of unknown
    }

    private PropertyDrift? ExtractPropertyDriftFromWhatIfLine(string line)
    {
        // Extract property drift from lines like:
        // "    ~ properties.tags.environment: \"dev\" => \"production\""
        // "    + properties.tags.newTag: \"value\""
        // "    - properties.tags.oldTag: \"value\""
        // "    x properties.features.enableSomething: true"
        
        var trimmedLine = line.Trim();
        if (trimmedLine.Length < 2)
        {
            return null;
        }
        
        var symbol = trimmedLine[0];
        var content = trimmedLine.Substring(1).Trim();
        
        // Skip "x" (no effect) lines as they don't represent actual drift
        if (symbol == 'x')
        {
            return null;
        }
        
        var colonIndex = content.IndexOf(':');
        if (colonIndex == -1)
        {
            return null;
        }
        
        var propertyPath = content.Substring(0, colonIndex).Trim();
        var valuesPart = content.Substring(colonIndex + 1).Trim();
        
        DriftType driftType;
        string expectedValue;
        string actualValue;
        
        if (symbol == '~')
        {
            // Modified property - extract current => desired (Azure what-if shows current => template)
            var arrowIndex = valuesPart.IndexOf("=>");
            if (arrowIndex != -1)
            {
                // What-if shows: current_azure_value => template_value
                // For drift detection: Expected = template_value, Actual = current_azure_value
                actualValue = valuesPart.Substring(0, arrowIndex).Trim().Trim('"');
                expectedValue = valuesPart.Substring(arrowIndex + 2).Trim().Trim('"');
                driftType = DriftType.Modified;
            }
            else
            {
                // Complex object or array without simple before/after
                // Show a more helpful message
                if (valuesPart.StartsWith("[") || valuesPart.StartsWith("{"))
                {
                    expectedValue = "configured in template";
                    actualValue = "configuration differs (details will be analyzed)";
                }
                else
                {
                    expectedValue = valuesPart.Trim('"');
                    actualValue = "modified";
                }
                driftType = DriftType.Modified;
            }
        }
        else if (symbol == '+')
        {
            // Added property
            // Skip empty or whitespace-only values that are likely structural Azure defaults
            if (string.IsNullOrWhiteSpace(valuesPart) || valuesPart.Trim() == "\"\"" || valuesPart.Trim() == "{}")
            {
                return null; // Skip these structural empty additions
            }
            
            expectedValue = "not set";
            actualValue = valuesPart.Trim('"');
            driftType = DriftType.Added;
        }
        else if (symbol == '-')
        {
            // Removed property - this means the property exists in Azure but will be deleted by template
            // So it's an "extra" property in Azure that's not wanted by the template
            // Skip empty or whitespace-only values that are likely structural Azure defaults
            if (string.IsNullOrWhiteSpace(valuesPart) || valuesPart.Trim() == "\"\"" || valuesPart.Trim() == "{}")
            {
                return null; // Skip these structural empty removals
            }
            
            expectedValue = "not set";
            actualValue = valuesPart.Trim('"');
            driftType = DriftType.Added;
        }
        else
        {
            return null;
        }
        
        return new PropertyDrift
        {
            PropertyPath = propertyPath,
            ExpectedValue = expectedValue,
            ActualValue = actualValue,
            Type = driftType
        };
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

    private void UpdateExpectedValueForComplexObject(PropertyDrift propertyDrift)
    {
        if (propertyDrift.PropertyPath.Contains("securityRules"))
        {
            propertyDrift.ExpectedValue = "Rule should exist as configured in template";
        }
        else if (propertyDrift.PropertyPath.Contains("subnets"))
        {
            propertyDrift.ExpectedValue = "Subnet should exist as configured in template";
        }
        else
        {
            propertyDrift.ExpectedValue = "Template configuration";
        }
    }

    private string FormatComplexObjectDetails(List<string> details, string propertyPath)
    {
        // Simple approach for NSG rules - always return clear message
        if (propertyPath.Contains("securityRules"))
        {
            // Try to extract ALL rule names from details
            var allText = string.Join("\n", details);
            var nameMatches = System.Text.RegularExpressions.Regex.Matches(allText, @"name:\s*""([^""]+)""");
            
            var ruleNames = nameMatches.Cast<System.Text.RegularExpressions.Match>()
                                       .Select(m => m.Groups[1].Value)
                                       .Distinct()
                                       .ToList();
            
            // Check if this is an addition (missing rule)
            if (allText.Contains("+ ") || allText.StartsWith("+"))
            {
                if (ruleNames.Count > 1)
                {
                    return $"{ruleNames.Count} security rules are missing from Azure: {string.Join(", ", ruleNames.Select(n => $"'{n}'"))}";
                }
                else if (ruleNames.Count == 1)
                {
                    return $"Security rule '{ruleNames[0]}' is missing from Azure";
                }
                return "Security rule(s) are missing from Azure";
            }
            // Check if this is a removal (extra rule) 
            else if (allText.Contains("- ") || allText.StartsWith("-"))
            {
                if (ruleNames.Count > 1)
                {
                    return $"{ruleNames.Count} extra security rules exist in Azure (not in template): {string.Join(", ", ruleNames.Select(n => $"'{n}'"))}";
                }
                else if (ruleNames.Count == 1)
                {
                    return $"Extra security rule '{ruleNames[0]}' exists in Azure (not in template)";
                }
                return "Extra security rule(s) exist in Azure (not in template)";
            }
            // Modified rule
            else
            {
                if (ruleNames.Count > 0)
                {
                    return $"Security rule '{ruleNames[0]}' configuration differs from template";
                }
                return "Security rule configuration differs from template";
            }
        }
        
        if (details.Count == 0) return "differs in Azure (complex object/array)";
        
        // Filter out structural characters like ]
        var meaningfulDetails = details.Where(d => !string.IsNullOrWhiteSpace(d) && d.Trim() != "]" && d.Trim() != "[").ToList();
        
        if (meaningfulDetails.Count == 0) return "differs in Azure (complex object/array)";
        
        // Check if this is an array change with items being added/removed
        var firstLine = meaningfulDetails.FirstOrDefault()?.Trim() ?? "";
        
        // Simple approach: Check what type of change this is
        if (firstLine.StartsWith("+"))
        {
            return "Missing from Azure (will be added by template)";
        }
        
        if (firstLine.StartsWith("-"))
        {
            return "Extra configuration exists in Azure (not in template)";
        }
        
        if (firstLine.StartsWith("~"))
        {
            return "Configuration differs from template";
        }
        
        // Check for array index patterns like "+ 0:", "- 1:", "~ 2:", etc.
        var indexPattern = System.Text.RegularExpressions.Regex.Match(firstLine, @"^([+\-~])\s*(\d+):\s*$");
        
        if (indexPattern.Success)
        {
            var symbol = indexPattern.Groups[1].Value[0];
            var index = indexPattern.Groups[2].Value;
            string action;
            
            if (symbol == '+')
            {
                action = "Missing in Azure (will be added)";
            }
            else if (symbol == '-')
            {
                action = "Extra in Azure (will be removed)";
            }
            else // symbol == '~'
            {
                action = "Modified in Azure";
            }
            
            if (propertyPath.Contains("securityRules"))
            {
                var ruleIdentifier = $"security rule #{index}";
                
                // Try to extract rule name from the details
                var namePattern = meaningfulDetails.FirstOrDefault(d => d.Contains("name:"));
                if (namePattern != null)
                {
                    var parts = namePattern.Split('\"');
                    if (parts.Length >= 2)
                    {
                        ruleIdentifier = $"security rule '{parts[1]}'";
                    }
                }
                
                // For modifications, extract what changed
                if (symbol == '~')
                {
                    // Look for property changes (lines with =>)
                    var propertyChanges = meaningfulDetails.Skip(1).Where(d => d.Contains("=>")).ToList();
                    
                    if (propertyChanges.Count == 1)
                    {
                        // Single property change
                        var change = propertyChanges[0].Trim();
                        if (change.StartsWith("~"))
                        {
                            change = change.Substring(1).Trim();
                        }
                        
                        var arrowIndex = change.IndexOf("=>");
                        if (arrowIndex > 0)
                        {
                            var colonIndex = change.IndexOf(":");
                            if (colonIndex > 0 && colonIndex < arrowIndex)
                            {
                                var propName = change.Substring(0, colonIndex).Trim();
                                var oldValue = change.Substring(colonIndex + 1, arrowIndex - colonIndex - 1).Trim();
                                var newValue = change.Substring(arrowIndex + 2).Trim();
                                return $"{action} {ruleIdentifier}: {propName} changed from {oldValue} to {newValue}";
                            }
                        }
                    }
                    else if (propertyChanges.Count > 1)
                    {
                        // Multiple properties changed
                        var changeDescriptions = new List<string> { $"{action} {ruleIdentifier}:" };
                        foreach (var change in propertyChanges)
                        {
                            var cleanChange = change.Trim();
                            if (cleanChange.StartsWith("~")) cleanChange = cleanChange.Substring(1).Trim();
                            changeDescriptions.Add($"  • {cleanChange}");
                        }
                        return string.Join("\n", changeDescriptions);
                    }
                }
                
                // For additions, show a clear summary instead of raw details
                if (symbol == '+')
                {
                    // Extract the actual rule name from details if possible
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(string.Join("", meaningfulDetails), @"name:\s*""([^""]+)""");
                    if (nameMatch.Success)
                    {
                        var ruleName = nameMatch.Groups[1].Value;
                        return $"Security rule '{ruleName}' is missing from Azure (will be added by template)";
                    }
                    else
                    {
                        return "Security rule is missing from Azure (will be added by template)";
                    }
                }
                
                return $"{action} {ruleIdentifier}:\n" + string.Join("\n", meaningfulDetails.Skip(1));
            }
            else if (propertyPath.Contains("subnets"))
            {
                return $"{action} subnet #{index}:\n" + string.Join("\n", meaningfulDetails.Skip(1));
            }
            else
            {
                return $"{action} item #{index}:\n" + string.Join("\n", meaningfulDetails.Skip(1));
            }
        }
        
        // For other complex changes, just return the details
        return "Azure configuration differs:\n" + string.Join("\n", meaningfulDetails);
    }
}