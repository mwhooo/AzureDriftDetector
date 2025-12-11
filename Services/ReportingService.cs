using AzureDriftDetector.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureDriftDetector.Services;

public class ReportingService
{
    public async Task GenerateReportAsync(DriftDetectionResult result, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.Console:
                GenerateConsoleReport(result);
                break;
            case OutputFormat.Json:
                await GenerateJsonReportAsync(result);
                break;
            case OutputFormat.Html:
                await GenerateHtmlReportAsync(result);
                break;
            case OutputFormat.Markdown:
                await GenerateMarkdownReportAsync(result);
                break;
        }
    }

    private void GenerateConsoleReport(DriftDetectionResult result)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{(simpleOutput ? "[DRIFT REPORT]" : "🔍")} AZURE CONFIGURATION DRIFT DETECTION REPORT");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{(simpleOutput ? "[TIME]" : "📅")} Detection Time: {result.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"{(simpleOutput ? "[SUMMARY]" : "📊")} Summary: {result.Summary}");
        Console.WriteLine();

        if (!result.HasDrift)
        {
            Console.WriteLine($"{(simpleOutput ? "[OK]" : "✅")} No configuration drift detected! All resources match their expected configuration.");
            return;
        }

        Console.WriteLine($"{(simpleOutput ? "[DRIFT]" : "❌")} Configuration drift detected in {result.ResourceDrifts.Count} resource(s):");
        Console.WriteLine();

        foreach (var resourceDrift in result.ResourceDrifts)
        {
            Console.WriteLine($"{(simpleOutput ? "[RESOURCE]" : "🔴")} {resourceDrift.ResourceType} - {resourceDrift.ResourceName}");
            Console.WriteLine($"   Property Drifts: {resourceDrift.PropertyDrifts.Count}");
            Console.WriteLine();

            foreach (var propertyDrift in resourceDrift.PropertyDrifts)
            {
                var icon = simpleOutput ? propertyDrift.Type switch
                {
                    DriftType.Missing => "[MISSING]",
                    DriftType.Extra => "[EXTRA]", 
                    DriftType.Modified => "[CHANGED]",
                    _ => "[UNKNOWN]"
                } : propertyDrift.Type switch
                {
                    DriftType.Missing => "❌",
                    DriftType.Extra => "➕", 
                    DriftType.Modified => "🔄",
                    _ => "❓"
                };

                Console.WriteLine($"   {icon} {propertyDrift.PropertyPath} ({propertyDrift.Type})");
                WriteAlignedValue("Expected", propertyDrift.ExpectedValue);
                WriteAlignedValue("Actual", propertyDrift.ActualValue);
                Console.WriteLine();
            }
        }

        Console.WriteLine(new string('=', 60));
    }

    private void WriteAlignedValue(string label, object? value)
    {
        const string indent = "      ";  // 6 spaces for base alignment
        const int labelWidth = 10;       // "Expected: " and "Actual:   " aligned
        
        var formattedLabel = label == "Expected" ? "Expected:" : "Actual:  ";
        var formattedValue = FormatValueForConsole(value);
        
        if (formattedValue.Contains('\n'))
        {
            // Multi-line value - put on next line with proper indentation
            Console.WriteLine($"{indent}{formattedLabel}");
            var lines = formattedValue.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.WriteLine($"{indent}  {line.TrimEnd()}");
                }
            }
        }
        else
        {
            // Single line value - inline
            Console.WriteLine($"{indent}{formattedLabel} {formattedValue}");
        }
    }

    private string FormatValueForConsole(object? value)
    {
        if (value == null) return "null";
        
        if (value is string str)
        {
            // Check if it's JSON
            if ((str.StartsWith("{") && str.EndsWith("}")) || 
                (str.StartsWith("[") && str.EndsWith("]")))
            {
                try
                {
                    var parsed = JToken.Parse(str);
                    return parsed.ToString(Formatting.Indented);
                }
                catch
                {
                    // Not valid JSON, return as-is
                }
            }
            
            // Truncate long strings
            if (str.Length > 80)
            {
                return $"\"{str.Substring(0, 77)}...\"";
            }
            
            return str == "not set" ? "not set" : $"\"{str}\"";
        }
        
        // For objects/arrays, serialize with indentation
        return JsonConvert.SerializeObject(value, Formatting.Indented);
    }

    private async Task GenerateJsonReportAsync(DriftDetectionResult result)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        var json = JsonConvert.SerializeObject(result, Formatting.Indented);
        var fileName = $"drift-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        
        await File.WriteAllTextAsync(fileName, json);
        // Print JSON to stdout so it can be captured by tools/scripts
        Console.WriteLine(json);
        Console.WriteLine($"\n{(simpleOutput ? "[JSON]" : "📄")} JSON report saved to: {fileName}");
    }

    private async Task GenerateHtmlReportAsync(DriftDetectionResult result)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        var html = GenerateHtmlContent(result);
        var fileName = $"drift-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html";
        
        await File.WriteAllTextAsync(fileName, html);
        Console.WriteLine($"{(simpleOutput ? "[HTML]" : "🌐")} HTML report saved to: {fileName}");
    }

    private async Task GenerateMarkdownReportAsync(DriftDetectionResult result)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        var markdown = GenerateMarkdownContent(result);
        var fileName = $"drift-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
        
        await File.WriteAllTextAsync(fileName, markdown);
        Console.WriteLine($"{(simpleOutput ? "[MD]" : "📝")} Markdown report saved to: {fileName}");
    }

    private string GenerateHtmlContent(DriftDetectionResult result)
    {
        var statusClass = result.HasDrift ? "drift-detected" : "no-drift";
        var statusIcon = result.HasDrift ? "❌" : "✅";
        var content = result.HasDrift ? GenerateHtmlDriftDetails(result) : 
            "<div class='summary'><h2>✅ No Configuration Drift Detected</h2><p>All resources match their expected configuration.</p></div>";
        
        return $@"<!DOCTYPE html>
<html>
<head>
    <title>Azure Drift Detection Report</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 40px; }}
        .header {{ border-bottom: 2px solid #007acc; padding-bottom: 20px; margin-bottom: 30px; }}
        .summary {{ background: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 30px; }}
        .resource {{ border: 1px solid #dee2e6; border-radius: 8px; margin-bottom: 20px; }}
        .resource-header {{ background: #f8f9fa; padding: 15px; border-bottom: 1px solid #dee2e6; }}
        .property-drift {{ padding: 15px; border-bottom: 1px solid #f1f3f4; }}
        .property-drift:last-child {{ border-bottom: none; }}
        .drift-detected {{ color: #dc3545; }}
        .no-drift {{ color: #28a745; }}
        .missing {{ background: #f8d7da; }}
        .extra {{ background: #d1ecf1; }}
        .modified {{ background: #fff3cd; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{statusIcon} Azure Configuration Drift Detection Report</h1>
        <p><strong>Detection Time:</strong> {result.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
        <p><strong>Summary:</strong> <span class='{statusClass}'>{result.Summary}</span></p>
    </div>
    
    {content}
</body>
</html>";
    }

    private string GenerateHtmlDriftDetails(DriftDetectionResult result)
    {
        var html = "<div class='resources'><h2>Resources with Configuration Drift</h2>";
        
        foreach (var resource in result.ResourceDrifts)
        {
            html += $@"
            <div class='resource'>
                <div class='resource-header'>
                    <h3>{resource.ResourceType} - {resource.ResourceName}</h3>
                    <p><strong>Resource ID:</strong> {resource.ResourceId}</p>
                    <p><strong>Property Drifts:</strong> {resource.PropertyDrifts.Count}</p>
                </div>";

            foreach (var drift in resource.PropertyDrifts)
            {
                var driftClass = drift.Type.ToString().ToLower();
                html += $@"
                <div class='property-drift {driftClass}'>
                    <h4>{drift.PropertyPath} ({drift.Type})</h4>
                    <p><strong>Expected:</strong> {FormatValue(drift.ExpectedValue)}</p>
                    <p><strong>Actual:</strong> {FormatValue(drift.ActualValue)}</p>
                </div>";
            }

            html += "</div>";
        }

        return html + "</div>";
    }

    private string GenerateMarkdownContent(DriftDetectionResult result)
    {
        var statusIcon = result.HasDrift ? "❌" : "✅";
        
        var markdown = $"""
        # {statusIcon} Azure Configuration Drift Detection Report
        
        **Detection Time:** {result.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC  
        **Summary:** {result.Summary}
        
        """;

        if (!result.HasDrift)
        {
            markdown += "## ✅ No Configuration Drift Detected\n\nAll resources match their expected configuration.\n";
            return markdown;
        }

        markdown += "## Resources with Configuration Drift\n\n";

        foreach (var resource in result.ResourceDrifts)
        {
            markdown += $"""
            ### 🔴 {resource.ResourceType} - {resource.ResourceName}
            
            **Resource ID:** `{resource.ResourceId}`  
            **Property Drifts:** {resource.PropertyDrifts.Count}
            
            """;

            foreach (var drift in resource.PropertyDrifts)
            {
                var icon = drift.Type switch
                {
                    DriftType.Missing => "❌",
                    DriftType.Extra => "➕",
                    DriftType.Modified => "🔄",
                    _ => "❓"
                };

                markdown += $"""
                #### {icon} {drift.PropertyPath} ({drift.Type})
                
                - **Expected:** `{FormatValue(drift.ExpectedValue)}`
                - **Actual:** `{FormatValue(drift.ActualValue)}`
                
                """;
            }
        }

        return markdown;
    }

    private string FormatValue(object? value)
    {
        if (value == null) return "null";
        
        // Special handling for multiline strings (from what-if details)
        if (value is string str)
        {
            if (str.Contains('\n'))
            {
                // Format multiline output with proper indentation
                var lines = str.Split('\n');
                var formattedLines = new List<string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd();
                    if (string.IsNullOrWhiteSpace(line) || line.Trim() == "]" || line.Trim() == "[") continue;
                    
                    if (i == 0)
                    {
                        formattedLines.Add(line);
                    }
                    else
                    {
                        formattedLines.Add($"               {line}");
                    }
                }
                return string.Join(Environment.NewLine, formattedLines);
            }
            return $"\"{str}\"";
        }
        
        return value switch
        {
            List<object?> list when IsSubnetArray(list) => FormatSubnetArray(list),
            Dictionary<string, object?> dict => FormatJsonWithIndentation(dict, 6),
            List<object?> list => FormatJsonWithIndentation(list, 6),
            _ => value.ToString() ?? "null"
        };
    }

    private string FormatJsonWithIndentation(object obj, int baseIndent)
    {
        var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
        var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Add base indentation to each line (except the first one which is already positioned)
        for (int i = 1; i < lines.Length; i++)
        {
            lines[i] = new string(' ', baseIndent) + lines[i];
        }
        
        return string.Join(Environment.NewLine, lines);
    }

    private bool IsSubnetArray(List<object?> list)
    {
        // Check if this looks like a subnet array by examining the first item
        if (!list.Any()) return false;
        
        var first = list.First();
        
        // Handle both Dictionary and JObject types
        if (first is Dictionary<string, object?> dict)
        {
            return dict.ContainsKey("name") && dict.ContainsKey("properties");
        }
        else if (first is Newtonsoft.Json.Linq.JObject jobj)
        {
            return jobj.ContainsKey("name") && jobj.ContainsKey("properties");
        }
        
        return false;
    }

    private string FormatSubnetArray(List<object?> subnets)
    {
        var formattedSubnets = new List<string>();

        foreach (var subnet in subnets)
        {
            string name = "unknown";
            string addressPrefix = "unknown";
            var serviceEndpoints = new List<string>();
            
            // Handle both Dictionary and JObject types
            if (subnet is Dictionary<string, object?> subnetDict)
            {
                name = subnetDict.GetValueOrDefault("name")?.ToString() ?? "unknown";

                if (subnetDict.TryGetValue("properties", out var properties) &&
                    properties is Dictionary<string, object?> propsDict)
                {
                    addressPrefix = propsDict.GetValueOrDefault("addressPrefix")?.ToString() ?? "unknown";
                    
                    // Check for service endpoints
                    if (propsDict.TryGetValue("serviceEndpoints", out var endpoints) &&
                        endpoints is List<object?> endpointList)
                    {
                        serviceEndpoints = ExtractServiceEndpointServices(endpointList);
                    }
                }
            }
            else if (subnet is Newtonsoft.Json.Linq.JObject jobj)
            {
                name = jobj["name"]?.ToString() ?? "unknown";
                var properties = jobj["properties"];
                if (properties != null)
                {
                    addressPrefix = properties["addressPrefix"]?.ToString() ?? "unknown";
                    
                    // Check for service endpoints
                    var endpoints = properties["serviceEndpoints"];
                    if (endpoints is JArray endpointsArray)
                    {
                        serviceEndpoints = ExtractServiceEndpointServices(endpointsArray.ToObject<List<object?>>() ?? new List<object?>());
                    }
                }
            }

            var subnetInfo = $"'{name}' ({addressPrefix})";
            if (serviceEndpoints.Any())
            {
                subnetInfo += $" [endpoints: {string.Join(", ", serviceEndpoints)}]";
            }
            
            formattedSubnets.Add(subnetInfo);
        }

        return $"[{string.Join(", ", formattedSubnets)}]";
    }

    private List<string> ExtractServiceEndpointServices(List<object?> endpoints)
    {
        var services = new List<string>();
        
        foreach (var endpoint in endpoints)
        {
            if (endpoint is Dictionary<string, object?> endpointDict &&
                endpointDict.TryGetValue("service", out var service))
            {
                services.Add(service?.ToString() ?? "unknown");
            }
            else if (endpoint is JObject endpointJObj)
            {
                var serviceName = endpointJObj["service"]?.ToString();
                if (!string.IsNullOrEmpty(serviceName))
                {
                    services.Add(serviceName);
                }
            }
        }
        
        return services;
    }
}