using System.Text.Json;
using System.Text.RegularExpressions;
using AzureDriftDetector.Models;

namespace AzureDriftDetector.Services;

public class DriftIgnoreService
{
    private readonly DriftIgnoreConfiguration _ignoreConfig;

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

        foreach (var resourceDrift in originalResult.ResourceDrifts)
        {
            var filteredPropertyDrifts = new List<PropertyDrift>();

            foreach (var propertyDrift in resourceDrift.PropertyDrifts)
            {
                totalDriftCount++;
                
                if (!ShouldIgnorePropertyDrift(resourceDrift, propertyDrift))
                {
                    filteredPropertyDrifts.Add(propertyDrift);
                }
                else
                {
                    ignoredCount++;
                    Console.WriteLine($"🔇 Ignoring drift: {resourceDrift.ResourceType}/{resourceDrift.ResourceName} - {propertyDrift.PropertyPath}");
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

    private bool ShouldIgnorePropertyDrift(ResourceDrift resourceDrift, PropertyDrift propertyDrift)
    {
        // Check global patterns first
        foreach (var globalPattern in _ignoreConfig.IgnorePatterns.GlobalPatterns)
        {
            if (MatchesPattern(propertyDrift.PropertyPath, globalPattern.PropertyPattern))
            {
                return true;
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
                    return true;
                }
            }
        }

        return false;
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