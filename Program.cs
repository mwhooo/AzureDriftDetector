using AzureDriftDetector.Core;
using AzureDriftDetector.Models;
using System.CommandLine;

namespace AzureDriftDetector;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure Configuration Drift Detector")
        {
            Description = "Compares Bicep/ARM templates with live Azure resources to detect configuration drift"
        };

        // Bicep file option
        var bicepFileOption = new Option<FileInfo>(
            name: "--bicep-file",
            description: "Path to the Bicep template file");
        bicepFileOption.IsRequired = true;

        // Resource group option
        var resourceGroupOption = new Option<string>(
            name: "--resource-group", 
            description: "Azure resource group name");
        resourceGroupOption.IsRequired = true;

        // Output format option
        var outputFormatOption = new Option<OutputFormat>(
            name: "--output",
            description: "Output format");
        outputFormatOption.IsRequired = false;
        outputFormatOption.SetDefaultValue(OutputFormat.Console);

        rootCommand.Add(bicepFileOption);
        rootCommand.Add(resourceGroupOption);
        rootCommand.Add(outputFormatOption);

        rootCommand.SetHandler(async (bicepFile, resourceGroup, outputFormat) =>
        {
            try
            {
                // Validate inputs
                if (!bicepFile.Exists)
                {
                    Console.WriteLine($"❌ Error: Bicep file not found: {bicepFile.FullName}");
                    Environment.Exit(1);
                    return;
                }

                if (string.IsNullOrWhiteSpace(resourceGroup))
                {
                    Console.WriteLine("❌ Error: Resource group name cannot be empty");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"🔍 Azure Configuration Drift Detector v1.0");
                Console.WriteLine($"📄 Bicep Template: {bicepFile.Name}");
                Console.WriteLine($"🏗️  Resource Group: {resourceGroup}");
                Console.WriteLine($"📊 Output Format: {outputFormat}");
                Console.WriteLine();

                var detector = new DriftDetector();
                var result = await detector.DetectDriftAsync(bicepFile, resourceGroup, outputFormat);
                
                if (result.HasDrift)
                {
                    Console.WriteLine("❌ Configuration drift detected!");
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine("✅ No configuration drift detected.");
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"💡 Ensure Azure CLI is installed and you're logged in with 'az login'");
                Environment.Exit(1);
            }
        }, bicepFileOption, resourceGroupOption, outputFormatOption);

        return await rootCommand.InvokeAsync(args);
    }
}
