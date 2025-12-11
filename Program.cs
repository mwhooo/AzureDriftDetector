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

        // Simple output option for better terminal compatibility
        var simpleOutputOption = new Option<bool>(
            name: "--simple-output",
            description: "Use simple ASCII characters instead of Unicode symbols");
        simpleOutputOption.IsRequired = false;
        simpleOutputOption.SetDefaultValue(false);

        // Autofix option to deploy template when drift is detected
        var autofixOption = new Option<bool>(
            name: "--autofix",
            description: "Automatically deploy the Bicep template to fix detected drift");
        autofixOption.IsRequired = false;
        autofixOption.SetDefaultValue(false);

        // Ignore configuration file option
        var ignoreConfigOption = new Option<FileInfo?>(
            name: "--ignore-config",
            description: "Path to drift ignore configuration file (default: drift-ignore.json)");
        ignoreConfigOption.IsRequired = false;

        // Show filtered drifts option for transparency/auditing
        var showFilteredOption = new Option<bool>(
            name: "--show-filtered",
            description: "Show details of filtered/ignored drifts for auditing purposes");
        showFilteredOption.IsRequired = false;
        showFilteredOption.SetDefaultValue(false);

        rootCommand.Add(bicepFileOption);
        rootCommand.Add(resourceGroupOption);
        rootCommand.Add(outputFormatOption);
        rootCommand.Add(simpleOutputOption);
        rootCommand.Add(autofixOption);
        rootCommand.Add(ignoreConfigOption);
        rootCommand.Add(showFilteredOption);

        rootCommand.SetHandler(async (bicepFile, resourceGroup, outputFormat, simpleOutput, autofix, ignoreConfig, showFiltered) =>
        {
            try
            {
                // Set up console encoding and simple output mode
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                
                // Store simple output preference globally for the reporting service
                Environment.SetEnvironmentVariable("SIMPLE_OUTPUT", simpleOutput.ToString());

                // Validate inputs
                if (!bicepFile.Exists)
                {
                    Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Error: Bicep file not found: {bicepFile.FullName}");
                    Environment.Exit(1);
                    return;
                }

                if (string.IsNullOrWhiteSpace(resourceGroup))
                {
                    Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Error: Resource group name cannot be empty");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"{(simpleOutput ? "[INFO]" : "🔍")} Azure Configuration Drift Detector v3.7.0");
                Console.WriteLine($"{(simpleOutput ? "[FILE]" : "📄")} Bicep Template: {bicepFile.Name}");
                Console.WriteLine($"{(simpleOutput ? "[RG]" : "🏗️")}  Resource Group: {resourceGroup}");
                Console.WriteLine($"{(simpleOutput ? "[OUTPUT]" : "📊")} Output Format: {outputFormat}");
                if (autofix)
                {
                    Console.WriteLine($"{(simpleOutput ? "[AUTOFIX]" : "🔧")} Autofix Mode: ENABLED");
                }
                
                // Show ignore config path being used
                var ignoreConfigPath = ignoreConfig?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), "drift-ignore.json");
                var ignoreExists = File.Exists(ignoreConfigPath);
                Console.WriteLine($"{(simpleOutput ? "[IGNORE]" : "🔇")} Ignore Config: {Path.GetFileName(ignoreConfigPath)} {(ignoreExists ? "(found)" : "(not found)")}");
                if (showFiltered)
                {
                    Console.WriteLine($"{(simpleOutput ? "[AUDIT]" : "🔍")} Show Filtered: ENABLED (audit mode)");
                }
                Console.WriteLine();

                // Set show-filtered preference globally
                Environment.SetEnvironmentVariable("SHOW_FILTERED", showFiltered.ToString());

                var detector = new DriftDetector(ignoreConfig?.FullName);
                var result = await detector.DetectDriftAsync(bicepFile, resourceGroup, outputFormat);
                
                if (result.HasDrift)
                {
                    Console.WriteLine($"{(simpleOutput ? "[DRIFT DETECTED]" : "❌")} Configuration drift detected!");
                    
                    if (autofix)
                    {
                        Console.WriteLine($"{(simpleOutput ? "[AUTOFIX]" : "🔧")} Attempting to fix drift by deploying template...");
                        var deploymentResult = await detector.DeployTemplateAsync(bicepFile, resourceGroup);
                        
                        if (deploymentResult.Success)
                        {
                            Console.WriteLine($"{(simpleOutput ? "[FIXED]" : "✅")} Drift has been automatically fixed!");
                            Console.WriteLine($"{(simpleOutput ? "[DEPLOYMENT]" : "📦")} Deployment Name: {deploymentResult.DeploymentName}");
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine($"{(simpleOutput ? "[AUTOFIX FAILED]" : "❌")} Failed to fix drift: {deploymentResult.ErrorMessage}");
                            Environment.Exit(1);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{(simpleOutput ? "[TIP]" : "💡")} Use --autofix to automatically deploy template and fix drift.");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    Console.WriteLine($"{(simpleOutput ? "[OK]" : "✅")} No configuration drift detected.");
                    Environment.Exit(0);
                }
            }
            catch (InvalidOperationException)
            {
                // Validation errors already have detailed output from BicepService
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{(simpleOutput ? "[FATAL]" : "❌")} Fatal error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"{(simpleOutput ? "[TIP]" : "💡")} Ensure Azure CLI is installed and you're logged in with 'az login'");
                Environment.Exit(1);
            }
        }, bicepFileOption, resourceGroupOption, outputFormatOption, simpleOutputOption, autofixOption, ignoreConfigOption, showFilteredOption);

        return await rootCommand.InvokeAsync(args);
    }
}
