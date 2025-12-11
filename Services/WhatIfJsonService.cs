using System.Diagnostics;
using System.Text.Json;
using AzureDriftDetector.Models;

namespace AzureDriftDetector.Services;

/// <summary>
/// Service to parse what-if JSON output for drift detection.
/// Uses --no-pretty-print to get structured JSON instead of text parsing.
/// </summary>
public class WhatIfJsonService
{
    private readonly DriftIgnoreService? _ignoreService;

    public WhatIfJsonService(DriftIgnoreService? ignoreService = null)
    {
        _ignoreService = ignoreService;
    }

    /// <summary>
    /// Runs az deployment what-if with JSON output and parses the results.
    /// </summary>
    public async Task<DriftDetectionResult> RunWhatIfAsync(string bicepFilePath, string resourceGroup)
    {
        try
        {
            var fileExtension = Path.GetExtension(bicepFilePath).ToLowerInvariant();
            string argumentsString;
            string templateFile;

            if (fileExtension == ".bicepparam")
            {
                var referencedBicepFile = await GetReferencedBicepFileAsync(bicepFilePath);
                templateFile = referencedBicepFile;
                argumentsString = $"deployment group what-if --resource-group \"{resourceGroup}\" --template-file \"{referencedBicepFile}\" --parameters \"{bicepFilePath}\" --no-prompt --no-pretty-print -o json";
            }
            else
            {
                templateFile = bicepFilePath;
                argumentsString = $"deployment group what-if --resource-group \"{resourceGroup}\" --template-file \"{bicepFilePath}\" --no-prompt --no-pretty-print -o json";
            }

            Console.WriteLine($"📋 Running what-if analysis (JSON mode)...");
            Console.WriteLine($"   Template: {Path.GetFileName(templateFile)}");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetAzureCLIPath(),
                    Arguments = argumentsString,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"❌ What-if command failed");
                Console.WriteLine($"   Error: {error}");
                throw new InvalidOperationException($"What-if failed: {error}");
            }

            Console.WriteLine($"✅ What-if analysis completed successfully");
            
            // Parse the JSON output
            var result = ParseWhatIfJson(output);
            
            // Apply ignore filters if configured
            if (_ignoreService != null)
            {
                result = _ignoreService.FilterIgnoredDrifts(result);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running what-if: {ex.Message}");
            throw;
        }
    }

    private DriftDetectionResult ParseWhatIfJson(string jsonOutput)
    {
        var result = new DriftDetectionResult();
        
        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // The what-if output has a "changes" array
            if (!root.TryGetProperty("changes", out var changes))
            {
                Console.WriteLine("📝 No changes found in what-if output");
                return result;
            }

            foreach (var change in changes.EnumerateArray())
            {
                var resourceDrift = ParseResourceChange(change);
                if (resourceDrift != null && resourceDrift.PropertyDrifts.Count > 0)
                {
                    result.ResourceDrifts.Add(resourceDrift);
                }
            }

            result.HasDrift = result.ResourceDrifts.Any();
            result.Summary = result.HasDrift
                ? $"Configuration drift detected in {result.ResourceDrifts.Count} resource(s) with {result.ResourceDrifts.Sum(r => r.PropertyDrifts.Count)} property difference(s)."
                : "No configuration drift detected.";

            Console.WriteLine($"📦 Parsed {result.ResourceDrifts.Count} resources with drift from what-if JSON");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ Failed to parse what-if JSON: {ex.Message}");
        }

        return result;
    }

    private ResourceDrift? ParseResourceChange(JsonElement change)
    {
        // Get the change type for the resource
        var changeType = change.GetProperty("changeType").GetString() ?? "";
        
        // Skip resources with no changes
        if (changeType == "NoChange" || changeType == "Ignore")
        {
            return null;
        }

        // Extract resource info
        var resourceId = change.TryGetProperty("resourceId", out var ridProp) ? ridProp.GetString() ?? "" : "";
        var (resourceType, resourceName) = ParseResourceId(resourceId);

        var drift = new ResourceDrift
        {
            ResourceType = resourceType,
            ResourceName = resourceName,
            ResourceId = resourceId,
            PropertyDrifts = new List<PropertyDrift>()
        };

        // Handle different change types
        switch (changeType)
        {
            case "Create":
                // Resource will be created (missing in Azure)
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "resource",
                    ExpectedValue = "defined in template",
                    ActualValue = "missing in Azure",
                    Type = DriftType.Missing
                });
                break;

            case "Delete":
                // Resource will be deleted (extra in Azure, not in template)
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "resource",
                    ExpectedValue = "not defined in template",
                    ActualValue = "exists in Azure",
                    Type = DriftType.Extra
                });
                break;

            case "Modify":
                // Resource has property changes
                if (change.TryGetProperty("delta", out var delta))
                {
                    ParsePropertyChanges(delta, drift);
                }
                break;

            case "Deploy":
                // Resource will be deployed but no property changes
                // This is often for nested deployments, we may want to skip
                break;
        }

        return drift;
    }

    private void ParsePropertyChanges(JsonElement delta, ResourceDrift drift)
    {
        foreach (var prop in delta.EnumerateArray())
        {
            var propertyChangeType = prop.TryGetProperty("propertyChangeType", out var pctProp) 
                ? pctProp.GetString() ?? "" 
                : "";
            
            // Skip NoEffect changes - they're not real drift
            if (propertyChangeType == "NoEffect")
            {
                continue;
            }

            var path = prop.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";
            var before = GetPropertyValue(prop, "before");
            var after = GetPropertyValue(prop, "after");
            var hasChildren = prop.TryGetProperty("children", out var children) && 
                              children.ValueKind == JsonValueKind.Array && 
                              children.GetArrayLength() > 0;

            // Skip container nodes and only process their children
            // Container nodes are:
            // 1. Array type changes (the array itself has changes, real changes are in children)
            // 2. Modify type changes with children and null before/after (intermediate path segments like "0", "5")
            bool isContainerNode = hasChildren && 
                                   (propertyChangeType == "Array" || 
                                    (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after)));
            
            if (isContainerNode)
            {
                ParseNestedPropertyChanges(children, drift, path);
                continue;
            }

            // Determine drift type based on change type
            var driftType = propertyChangeType switch
            {
                "Create" => DriftType.Missing,  // Template will add this (missing in Azure)
                "Delete" => DriftType.Extra,    // Template will remove this (extra in Azure)
                "Modify" => DriftType.Modified,
                "Array" => DriftType.Modified,  // Array without children (shouldn't happen often)
                _ => DriftType.Modified
            };

            // For Delete, swap before/after for clarity (what Azure has vs template expects)
            string expectedValue, actualValue;
            if (propertyChangeType == "Delete")
            {
                expectedValue = "not set";
                actualValue = before;
            }
            else if (propertyChangeType == "Create")
            {
                expectedValue = after;
                actualValue = "not set";
            }
            else
            {
                expectedValue = after;  // What template wants
                actualValue = before;   // What Azure has
            }

            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = path,
                ExpectedValue = expectedValue,
                ActualValue = actualValue,
                Type = driftType
            });

            // Recursively handle any remaining nested changes
            if (hasChildren)
            {
                ParseNestedPropertyChanges(children, drift, path);
            }
        }
    }

    private void ParseNestedPropertyChanges(JsonElement children, ResourceDrift drift, string parentPath)
    {
        foreach (var child in children.EnumerateArray())
        {
            var propertyChangeType = child.TryGetProperty("propertyChangeType", out var pctProp) 
                ? pctProp.GetString() ?? "" 
                : "";
            
            if (propertyChangeType == "NoEffect")
            {
                continue;
            }

            var path = child.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";
            var fullPath = string.IsNullOrEmpty(parentPath) ? path : $"{parentPath}.{path}";
            var before = GetPropertyValue(child, "before");
            var after = GetPropertyValue(child, "after");

            // Check if this has children
            bool hasNestedChildren = child.TryGetProperty("children", out var nestedChildren) && 
                                     nestedChildren.ValueKind == JsonValueKind.Array && 
                                     nestedChildren.GetArrayLength() > 0;

            // Skip container nodes and only process their children
            // Container nodes are:
            // 1. Array type changes (the array itself has changes, real changes are in children)
            // 2. Modify type changes with children and null before/after (intermediate path segments like "0", "5")
            bool isContainerNode = hasNestedChildren && 
                                   (propertyChangeType == "Array" || 
                                    (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after)));
            
            if (isContainerNode)
            {
                ParseNestedPropertyChanges(nestedChildren, drift, fullPath);
                continue;
            }

            var driftType = propertyChangeType switch
            {
                "Create" => DriftType.Missing,
                "Delete" => DriftType.Extra,
                "Modify" => DriftType.Modified,
                "Array" => DriftType.Modified,
                _ => DriftType.Modified
            };

            string expectedValue, actualValue;
            if (propertyChangeType == "Delete")
            {
                expectedValue = "not set";
                actualValue = before;
            }
            else if (propertyChangeType == "Create")
            {
                expectedValue = after;
                actualValue = "not set";
            }
            else
            {
                expectedValue = after;
                actualValue = before;
            }

            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = fullPath,
                ExpectedValue = expectedValue,
                ActualValue = actualValue,
                Type = driftType
            });

            // Continue recursively for non-container types that might still have children
            if (hasNestedChildren)
            {
                ParseNestedPropertyChanges(nestedChildren, drift, fullPath);
            }
        }
    }

    private string GetPropertyValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return "";
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            JsonValueKind.Object => prop.GetRawText(),
            JsonValueKind.Array => prop.GetRawText(),
            _ => prop.GetRawText()
        };
    }

    private (string resourceType, string resourceName) ParseResourceId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return ("Unknown", "Unknown");
        }

        // Resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var parts = resourceId.Split('/');
        
        // Find the providers index
        var providersIndex = Array.IndexOf(parts, "providers");
        if (providersIndex < 0 || providersIndex + 3 > parts.Length)
        {
            return ("Unknown", resourceId);
        }

        // Extract resource type and name
        // Handle nested resources like Microsoft.Storage/storageAccounts/blobServices
        // Resource ID format after providers: Microsoft.Network/virtualNetworks/vnetName/subnets/subnetName
        // The pattern is: provider/type/name[/childType/childName]...
        var remainingParts = parts.Skip(providersIndex + 1).ToArray();
        
        if (remainingParts.Length >= 2)
        {
            // First part is always the provider namespace (e.g., Microsoft.Network)
            var provider = remainingParts[0];
            
            // Build type and name from remaining parts
            // Pattern: type/name/childType/childName/...
            var typeParts = new List<string> { provider };
            var nameParts = new List<string>();
            
            for (int i = 1; i < remainingParts.Length; i++)
            {
                if (i % 2 == 1)
                {
                    // Odd indices (1, 3, 5...) are types
                    typeParts.Add(remainingParts[i]);
                }
                else
                {
                    // Even indices (2, 4, 6...) are names
                    nameParts.Add(remainingParts[i]);
                }
            }
            
            return (string.Join("/", typeParts), string.Join("/", nameParts));
        }

        return ("Unknown", resourceId);
    }

    private async Task<string> GetReferencedBicepFileAsync(string bicepparamFilePath)
    {
        var content = await File.ReadAllTextAsync(bicepparamFilePath);
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using"))
            {
                // Extract the file path from: using 'file.bicep' or using './file.bicep'
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"using\s+'([^']+)'");
                if (match.Success)
                {
                    var referencedFile = match.Groups[1].Value;
                    var directory = Path.GetDirectoryName(bicepparamFilePath) ?? "";
                    return Path.GetFullPath(Path.Combine(directory, referencedFile));
                }
            }
        }
        
        throw new InvalidOperationException($"Could not find 'using' statement in {bicepparamFilePath}");
    }

    private string GetAzureCLIPath()
    {
        // Try common Azure CLI paths
        var possiblePaths = new[]
        {
            "az",
            "az.cmd",
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(5000);
                if (process?.ExitCode == 0)
                {
                    return path;
                }
            }
            catch
            {
                // Try next path
            }
        }

        return "az"; // Default, let it fail with a proper error message
    }
}
