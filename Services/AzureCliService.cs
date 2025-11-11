using System.Diagnostics;
using System.Text.Json;
using AzureDriftDetector.Models;
using Newtonsoft.Json.Linq;

namespace AzureDriftDetector.Services;

public class AzureCliService
{
    public async Task<List<AzureResource>> GetResourcesAsync(string resourceGroup)
    {
        try
        {
            // Query all resources in the resource group
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetAzureCLIPath(),
                    Arguments = $"resource list --resource-group \"{resourceGroup}\" --output json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    EnvironmentVariables = { ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "" }
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to query Azure resources: {error}");
            }

            var resourcesArray = JArray.Parse(output);
            var resources = new List<AzureResource>();

            foreach (var resource in resourcesArray)
            {
                if (resource is JObject resourceObj)
                {
                    // Get detailed resource information
                    var detailedResource = await GetResourceDetailsAsync(resourceObj["id"]?.ToString() ?? "");
                    if (detailedResource != null)
                    {
                        resources.Add(detailedResource);
                    }
                }
            }

            return resources;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error querying Azure resources in resource group '{resourceGroup}': {ex.Message}", ex);
        }
    }

    public async Task<AzureResource?> GetResourceDetailsAsync(string resourceId)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetAzureCLIPath(),
                    Arguments = $"resource show --id \"{resourceId}\" --output json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    EnvironmentVariables = { ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "" }
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
                Console.WriteLine($"{(simpleOutput ? "[WARN]" : "⚠️")}  Warning: Could not get details for resource {resourceId}: {error}");
                return null;
            }

            var resourceJson = JObject.Parse(output);
            
            return new AzureResource
            {
                Type = resourceJson["type"]?.ToString() ?? "",
                Name = resourceJson["name"]?.ToString() ?? "",
                Id = resourceJson["id"]?.ToString() ?? "",
                Properties = ParseProperties(resourceJson)
            };
        }
        catch (Exception ex)
        {
            bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
            Console.WriteLine($"{(simpleOutput ? "[WARN]" : "⚠️")}  Warning: Error getting resource details for {resourceId}: {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, object?> ParseProperties(JObject resourceJson)
    {
        var properties = new Dictionary<string, object?>();

        // Extract key properties that are commonly compared
        if (resourceJson["properties"] is JObject props)
        {
            foreach (var prop in props)
            {
                properties[$"properties.{prop.Key}"] = ParseJToken(prop.Value);
            }
        }

        // Add other important fields at root level
        properties["type"] = resourceJson["type"]?.ToString();
        properties["location"] = resourceJson["location"]?.ToString();
        properties["sku"] = ParseJToken(resourceJson["sku"]);
        properties["tags"] = ParseJToken(resourceJson["tags"]);
        
        // Add storage account specific properties
        properties["publicNetworkAccess"] = resourceJson["publicNetworkAccess"]?.ToString();
        properties["networkRuleSet"] = ParseJToken(resourceJson["networkRuleSet"]);
        properties["allowBlobPublicAccess"] = resourceJson["allowBlobPublicAccess"]?.ToString();
        properties["minimumTlsVersion"] = resourceJson["minimumTlsVersion"]?.ToString();

        return properties;
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

    private static string GetAzureCLIPath()
    {
        // First, try to find az using 'where' command (most reliable on Windows)
        try
        {
            using var whereProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "az",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            whereProcess.Start();
            var output = whereProcess.StandardOutput.ReadToEnd();
            whereProcess.WaitForExit(5000);
            
            if (whereProcess.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var paths = output.Trim().Split('\n', '\r');
                // Prefer .cmd files over batch files, filter out empty lines
                var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                var preferredPath = validPaths.FirstOrDefault(p => p.Trim().EndsWith(".cmd")) ?? validPaths.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(preferredPath))
                {
                    return preferredPath.Trim();
                }
            }
        }
        catch
        {
            // Fall back to manual search
        }

        // Try common Azure CLI locations
        var possiblePaths = new[]
        {
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            "az",
            "az.exe",
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (IsCommandAvailable(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException(
            "Azure CLI not found. Please ensure Azure CLI is installed and accessible in PATH.\n" +
            "Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli");
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}