using System.Text.Json;
using System.Text.RegularExpressions;
using AzureDriftDetector.Models;

namespace AzureDriftDetector.Services;

public class DriftIgnoreService
{
    private readonly DriftIgnoreConfiguration _ignoreConfig;
    
    // Track which rules have been used for reporting unused rules
    private readonly HashSet<string> _usedResourceRules = new();
    private readonly HashSet<string> _usedGlobalPatterns = new();

    public DriftIgnoreService(string? ignoreConfigPath = null)
    {
        _ignoreConfig = LoadIgnoreConfiguration(ignoreConfigPath);
    }

    private DriftIgnoreConfiguration LoadIgnoreConfiguration(string? configPath)
    {
        try
        {
            // Default to drift-ignore.json in the current working directory
            configPath ??= Path.Combine(Directory.GetCurrentDirectory(), "drift-ignore.json");
            
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"⚠️  No ignore configuration found at: {configPath}");
                return new DriftIgnoreConfiguration();
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<DriftIgnoreConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Console.WriteLine($"📋 Loaded ignore configuration with {config?.IgnorePatterns.Resources.Count ?? 0} resource rules and {config?.IgnorePatterns.GlobalPatterns.Count ?? 0} global patterns");
            
            return config ?? new DriftIgnoreConfiguration();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️  Warning: Invalid JSON in ignore configuration: {ex.Message}");
            return new DriftIgnoreConfiguration();
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"⚠️  Warning: Access denied reading ignore configuration: {ex.Message}");
            return new DriftIgnoreConfiguration();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Failed to load ignore configuration: {ex.Message}");
            return new DriftIgnoreConfiguration();
        }
    }

    public DriftDetectionResult FilterIgnoredDrifts(DriftDetectionResult originalResult)
    {
        var filteredResult = new DriftDetectionResult
        {
            DetectedAt = originalResult.DetectedAt
        };

        var ignoredCount = 0;
        var totalDriftCount = 0;
        var reportedIgnores = new HashSet<string>(); // Track what we've already reported
        var showFiltered = Environment.GetEnvironmentVariable("SHOW_FILTERED") == "True";

        foreach (var resourceDrift in originalResult.ResourceDrifts)
        {
            var filteredPropertyDrifts = new List<PropertyDrift>();

            foreach (var propertyDrift in resourceDrift.PropertyDrifts)
            {
                totalDriftCount++;
                
                var (shouldIgnore, ignoreReason) = ShouldIgnorePropertyDriftWithReason(resourceDrift, propertyDrift);
                
                if (!shouldIgnore)
                {
                    filteredPropertyDrifts.Add(propertyDrift);
                }
                else
                {
                    ignoredCount++;
                    // Deduplicate console output to avoid showing the same ignore message multiple times
                    var ignoreKey = $"{resourceDrift.ResourceType}/{resourceDrift.ResourceName}:{propertyDrift.PropertyPath}";
                    if (reportedIgnores.Add(ignoreKey))
                    {
                        if (showFiltered)
                        {
                            // Detailed output for audit mode
                            Console.WriteLine($"🔇 Ignoring drift: {resourceDrift.ResourceType}/{resourceDrift.ResourceName} - {propertyDrift.PropertyPath}");
                            Console.WriteLine($"   Reason: {ignoreReason}");
                            Console.WriteLine($"   Expected: {propertyDrift.ExpectedValue?.ToString()?.Substring(0, Math.Min(80, propertyDrift.ExpectedValue?.ToString()?.Length ?? 0))}...");
                            Console.WriteLine($"   Actual: {propertyDrift.ActualValue?.ToString()?.Substring(0, Math.Min(80, propertyDrift.ActualValue?.ToString()?.Length ?? 0))}...");
                        }
                        else
                        {
                            Console.WriteLine($"🔇 Ignoring drift: {resourceDrift.ResourceType}/{resourceDrift.ResourceName} - {propertyDrift.PropertyPath}");
                        }
                    }
                }
            }

            // Only add resource drift if it has remaining properties after filtering
            if (filteredPropertyDrifts.Count > 0)
            {
                filteredResult.ResourceDrifts.Add(new ResourceDrift
                {
                    ResourceType = resourceDrift.ResourceType,
                    ResourceName = resourceDrift.ResourceName,
                    ResourceId = resourceDrift.ResourceId,
                    PropertyDrifts = filteredPropertyDrifts
                });
            }
        }

        // Update summary
        var remainingDriftCount = totalDriftCount - ignoredCount;
        filteredResult.HasDrift = filteredResult.ResourceDrifts.Any();
        
        if (ignoredCount > 0)
        {
            Console.WriteLine($"📊 Filtered {ignoredCount} ignored drift(s) out of {totalDriftCount} total drift(s)");
        }

        // Report unused rules if any
        ReportUnusedRules();

        if (filteredResult.HasDrift)
        {
            filteredResult.Summary = $"Configuration drift detected in {filteredResult.ResourceDrifts.Count} resource(s) with {remainingDriftCount} property difference(s).";
        }
        else
        {
            filteredResult.Summary = ignoredCount > 0 
                ? $"No configuration drift detected after filtering {ignoredCount} ignored drift(s)."
                : "No configuration drift detected.";
        }

        return filteredResult;
    }

    private void ReportUnusedRules()
    {
        var showFiltered = Environment.GetEnvironmentVariable("SHOW_FILTERED") == "True";
        if (!showFiltered) return;

        var unusedResourceRules = new List<string>();
        var unusedGlobalPatterns = new List<string>();

        // Check for unused resource rules
        foreach (var resourceRule in _ignoreConfig.IgnorePatterns.Resources)
        {
            foreach (var property in resourceRule.IgnoredProperties)
            {
                var ruleKey = $"{resourceRule.ResourceType}:{property}";
                if (!_usedResourceRules.Contains(ruleKey))
                {
                    unusedResourceRules.Add($"{resourceRule.ResourceType} - {property}");
                }
            }
        }

        // Check for unused global patterns
        foreach (var globalPattern in _ignoreConfig.IgnorePatterns.GlobalPatterns)
        {
            if (!_usedGlobalPatterns.Contains(globalPattern.PropertyPattern))
            {
                unusedGlobalPatterns.Add(globalPattern.PropertyPattern);
            }
        }

        if (unusedResourceRules.Count > 0 || unusedGlobalPatterns.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"📋 Unused ignore rules (consider removing from drift-ignore.json):");
            
            foreach (var rule in unusedResourceRules)
            {
                Console.WriteLine($"   • Resource: {rule}");
            }
            
            foreach (var pattern in unusedGlobalPatterns)
            {
                Console.WriteLine($"   • Global: {pattern}");
            }
        }
    }

    private bool ShouldIgnorePropertyDrift(ResourceDrift resourceDrift, PropertyDrift propertyDrift)
    {
        var (shouldIgnore, _) = ShouldIgnorePropertyDriftWithReason(resourceDrift, propertyDrift);
        return shouldIgnore;
    }

    private (bool shouldIgnore, string reason) ShouldIgnorePropertyDriftWithReason(ResourceDrift resourceDrift, PropertyDrift propertyDrift)
    {
        // Check if this is an empty/structural comparison - not meaningful drift
        // These are container nodes in JSON arrays where the real changes are in children
        if (IsEmptyValueComparison(propertyDrift.ExpectedValue?.ToString(), propertyDrift.ActualValue?.ToString()))
        {
            return (true, "Empty/structural comparison - both values are empty (container node in nested JSON)");
        }

        // Check if this is an ARM template expression comparison
        // What-if shows unresolved ARM expressions like "[parameters('tenantId')]" 
        // while Azure has the actual resolved values.
        // 
        // IMPORTANT: This assumes the parameter/variable values haven't changed since deployment.
        // If you've changed parameter values, you should redeploy to apply those changes.
        // The what-if comparison is "what template WILL deploy" vs "what Azure HAS now".
        if (IsArmExpressionComparison(propertyDrift.ExpectedValue?.ToString(), propertyDrift.ActualValue?.ToString()))
        {
            return (true, "ARM template expression - expected value is unresolved ARM function (will resolve at deployment)");
        }

        // Check global patterns first
        foreach (var globalPattern in _ignoreConfig.IgnorePatterns.GlobalPatterns)
        {
            if (MatchesPattern(propertyDrift.PropertyPath, globalPattern.PropertyPattern))
            {
                _usedGlobalPatterns.Add(globalPattern.PropertyPattern);
                return (true, $"Global pattern match: {globalPattern.PropertyPattern} - {globalPattern.Reason}");
            }
        }

        // Check resource-specific ignore rules
        foreach (var resourceRule in _ignoreConfig.IgnorePatterns.Resources)
        {
            if (!MatchesResourceType(resourceDrift.ResourceType, resourceRule.ResourceType))
            {
                continue;
            }

            // Check if conditions match (if any)
            if (resourceRule.Conditions.Count > 0)
            {
                // For now, we'll skip condition checking as it requires more context
                // In a full implementation, we'd need access to the full resource properties
                // to evaluate conditions like "skuTier": "Basic"
            }

            // Check if property is in the ignore list
            foreach (var ignoredProperty in resourceRule.IgnoredProperties)
            {
                if (MatchesPattern(propertyDrift.PropertyPath, ignoredProperty))
                {
                    _usedResourceRules.Add($"{resourceRule.ResourceType}:{ignoredProperty}");
                    return (true, $"Resource rule: {resourceRule.ResourceType} - {resourceRule.Reason}");
                }
            }
        }

        return (false, "");
    }

    private bool MatchesResourceType(string actualResourceType, string patternResourceType)
    {
        // Support wildcards in resource type matching
        if (patternResourceType.Contains('*'))
        {
            var regex = new Regex($"^{Regex.Escape(patternResourceType).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
            return regex.IsMatch(actualResourceType);
        }

        return string.Equals(actualResourceType, patternResourceType, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesPattern(string actualProperty, string pattern)
    {
        // Support wildcards in property matching
        if (pattern.Contains('*'))
        {
            var regex = new Regex($"^{Regex.Escape(pattern).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
            return regex.IsMatch(actualProperty);
        }

        return string.Equals(actualProperty, pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects if a drift is just an ARM template expression compared to its resolved value.
    /// What-if shows unresolved ARM expressions like "[parameters('foo')]" or "[reference(...)]"
    /// while Azure has the actual resolved values. These are not real drifts.
    /// </summary>
    private bool IsArmExpressionComparison(string expectedValue, string actualValue)
    {
        if (string.IsNullOrEmpty(expectedValue) || string.IsNullOrEmpty(actualValue))
        {
            return false;
        }

        // Check if expected value is an ARM template expression
        // ARM expressions start with '[' and end with ']'
        var trimmedExpected = expectedValue.Trim();
        if (trimmedExpected.StartsWith("[") && trimmedExpected.EndsWith("]"))
        {
            // Check for common ARM functions that get resolved to actual values
            var armFunctions = new[]
            {
                "[parameters(",
                "[reference(",
                "[variables(",
                "[concat(",
                "[format(",
                "[subscription(",
                "[resourceGroup(",
                "[resourceId(",
                "[createObject(",
                "[createArray(",
                "[union(",
                "[coalesce(",
                "[if(",
                "[uniqueString("
            };

            if (armFunctions.Any(f => trimmedExpected.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            {
                // This is an ARM expression being compared to a resolved value - not real drift
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects if both expected and actual values are empty or null.
    /// These are structural markers from nested JSON parsing, not meaningful drifts.
    /// </summary>
    private bool IsEmptyValueComparison(string? expectedValue, string? actualValue)
    {
        var expectedEmpty = string.IsNullOrWhiteSpace(expectedValue);
        var actualEmpty = string.IsNullOrWhiteSpace(actualValue);
        
        // If both are empty, this is a structural comparison, not real drift
        return expectedEmpty && actualEmpty;
    }

    public void AddIgnoreRule(string resourceType, string propertyPath, string reason)
    {
        // Find existing rule or create new one
        var existingRule = _ignoreConfig.IgnorePatterns.Resources
            .FirstOrDefault(r => string.Equals(r.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));

        if (existingRule != null)
        {
            if (!existingRule.IgnoredProperties.Contains(propertyPath, StringComparer.OrdinalIgnoreCase))
            {
                existingRule.IgnoredProperties.Add(propertyPath);
            }
        }
        else
        {
            _ignoreConfig.IgnorePatterns.Resources.Add(new ResourceIgnoreRule
            {
                ResourceType = resourceType,
                Reason = reason,
                IgnoredProperties = [propertyPath]
            });
        }
    }

    public void SaveIgnoreConfiguration(string? configPath = null)
    {
        try
        {
            configPath ??= Path.Combine(Directory.GetCurrentDirectory(), "drift-ignore.json");
            
            var json = JsonSerializer.Serialize(_ignoreConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(configPath, json);
            Console.WriteLine($"💾 Saved ignore configuration to: {configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error saving ignore configuration: {ex.Message}");
        }
    }
}