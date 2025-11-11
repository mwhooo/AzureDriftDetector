using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace AzureDriftDetector.Services;

public class BicepService
{
    public async Task<JObject> ConvertBicepToArmAsync(string bicepFilePath)
    {
        try
        {
            // Check if Bicep file exists
            if (!File.Exists(bicepFilePath))
            {
                throw new FileNotFoundException($"Bicep file not found: {bicepFilePath}");
            }

            // Use Azure CLI to build Bicep template to ARM JSON
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetAzureCLIPath(),
                    Arguments = $"bicep build --file \"{bicepFilePath}\" --stdout",
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
                throw new InvalidOperationException($"Failed to convert Bicep to ARM: {error}");
            }

            var armTemplate = JObject.Parse(output);
            return armTemplate;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error converting Bicep file '{bicepFilePath}' to ARM template: {ex.Message}", ex);
        }
    }

    public List<JObject> ExtractResourcesFromTemplate(JObject armTemplate)
    {
        var resources = new List<JObject>();
        
        if (armTemplate["resources"] is JArray resourceArray)
        {
            foreach (var resource in resourceArray)
            {
                if (resource is JObject resourceObj)
                {
                    // Check if resource has a condition and evaluate it
                    if (ShouldResourceBeDeployed(resourceObj, armTemplate))
                    {
                        resources.Add(resourceObj);
                    }
                }
            }
        }

        return resources;
    }

    private bool ShouldResourceBeDeployed(JObject resource, JObject armTemplate)
    {
        // If there's no condition property, the resource should always be deployed
        if (resource["condition"] == null)
        {
            return true;
        }

        var condition = resource["condition"]?.ToString();
        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        // Evaluate the condition expression
        return EvaluateConditionExpression(condition, armTemplate);
    }

    private bool EvaluateConditionExpression(string condition, JObject armTemplate)
    {
        try
        {
            // Handle simple variable references like "[variables('deploykeyvault')]"
            if (condition.StartsWith("[variables('") && condition.EndsWith("')]"))
            {
                var variableName = condition.Substring(12, condition.Length - 15); // Extract variable name
                var variables = armTemplate["variables"] as JObject;
                
                if (variables != null && variables[variableName] != null)
                {
                    var variableValue = variables[variableName];
                    
                    // Handle boolean values
                    if (variableValue?.Type == JTokenType.Boolean)
                    {
                        return variableValue.Value<bool>();
                    }
                    
                    // Handle string representations of boolean
                    if (variableValue?.Type == JTokenType.String)
                    {
                        var stringValue = variableValue.Value<string>()?.ToLowerInvariant();
                        return stringValue == "true";
                    }
                }
            }
            
            // Handle simple parameter references like "[parameters('deploykeyvault')]"
            if (condition.StartsWith("[parameters('") && condition.EndsWith("')]"))
            {
                var parameterName = condition.Substring(13, condition.Length - 16); // Extract parameter name
                var parameters = armTemplate["parameters"] as JObject;
                
                if (parameters != null && parameters[parameterName] != null)
                {
                    var parameter = parameters[parameterName] as JObject;
                    var defaultValue = parameter?["defaultValue"];
                    
                    if (defaultValue?.Type == JTokenType.Boolean)
                    {
                        return defaultValue.Value<bool>();
                    }
                    
                    if (defaultValue?.Type == JTokenType.String)
                    {
                        var stringValue = defaultValue.Value<string>()?.ToLowerInvariant();
                        return stringValue == "true";
                    }
                }
            }

            // For more complex expressions, default to true (deploy the resource)
            // This is a conservative approach - if we can't evaluate the condition,
            // we include the resource and let the comparison logic handle it
            Console.WriteLine($"⚠️  Could not evaluate condition: {condition}. Assuming resource should be deployed.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error evaluating condition '{condition}': {ex.Message}. Assuming resource should be deployed.");
            return true;
        }
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