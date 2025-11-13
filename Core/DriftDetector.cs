using AzureDriftDetector.Models;
using AzureDriftDetector.Services;
using Newtonsoft.Json;

namespace AzureDriftDetector.Core;

public class DriftDetector
{
    private readonly BicepService _bicepService;
    private readonly AzureCliService _azureCliService;
    private readonly ComparisonService _comparisonService;
    private readonly ReportingService _reportingService;

    public DriftDetector()
    {
        _bicepService = new BicepService();
        _azureCliService = new AzureCliService();
        _comparisonService = new ComparisonService();
        _reportingService = new ReportingService();
    }

    public async Task<DriftDetectionResult> DetectDriftAsync(
        FileInfo bicepFile, 
        string resourceGroup, 
        OutputFormat outputFormat = OutputFormat.Console)
    {
        Console.WriteLine($"🔍 Starting drift detection for resource group: {resourceGroup}");
        Console.WriteLine($"📄 Using Bicep template: {bicepFile.FullName}");

        try
        {
            // Step 1: Convert Bicep to ARM JSON template
            bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
            Console.WriteLine($"{(simpleOutput ? "[BICEP]" : "⚙️")}  Converting Bicep template to ARM JSON...");
            var expectedTemplate = await _bicepService.ConvertBicepToArmAsync(bicepFile.FullName);

            // Step 2: Query live Azure resources
            Console.WriteLine($"{(simpleOutput ? "[AZURE]" : "☁️")}  Querying live Azure resources...");
            var liveResources = await _azureCliService.GetResourcesAsync(resourceGroup);

            // Step 3: Compare expected vs actual
            Console.WriteLine("🔄 Comparing expected configuration with live resources...");
            var result = _comparisonService.CompareResources(expectedTemplate, liveResources);

            // Step 4: Generate report
            Console.WriteLine("📊 Generating drift report...");
            await _reportingService.GenerateReportAsync(result, outputFormat);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during drift detection: {ex.Message}");
            throw;
        }
    }

    public async Task<DeploymentResult> DeployTemplateAsync(FileInfo bicepFile, string resourceGroup)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        
        try
        {
            Console.WriteLine($"{(simpleOutput ? "[DEPLOY]" : "🚀")} Deploying Bicep template to resource group: {resourceGroup}");
            Console.WriteLine($"{(simpleOutput ? "[FILE]" : "📄")} Template file: {bicepFile.FullName}");

            var result = await _azureCliService.DeployBicepTemplateAsync(bicepFile.FullName, resourceGroup);
            
            if (result.Success)
            {
                Console.WriteLine($"{(simpleOutput ? "[SUCCESS]" : "✅")} Deployment completed successfully!");
            }
            else
            {
                Console.WriteLine($"{(simpleOutput ? "[FAILED]" : "❌")} Deployment failed!");
            }

            return result;
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Bicep file not found: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Bicep file not found: {ex.Message}"
            };
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Azure CLI error during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Azure CLI error: {ex.Message}"
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Access denied during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Access denied: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            // Catch any other unexpected exceptions to ensure we always return a structured result
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Unexpected error during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }
}