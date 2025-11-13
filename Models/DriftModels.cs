namespace AzureDriftDetector.Models;

public enum OutputFormat
{
    Console,
    Json,
    Html,
    Markdown
}

public class DriftDetectionResult
{
    public bool HasDrift { get; set; }
    public List<ResourceDrift> ResourceDrifts { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
}

public class ResourceDrift
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public List<PropertyDrift> PropertyDrifts { get; set; } = new();
    public bool HasDrift => PropertyDrifts.Any();
}

public class PropertyDrift
{
    public string PropertyPath { get; set; } = string.Empty;
    public object? ExpectedValue { get; set; }
    public object? ActualValue { get; set; }
    public DriftType Type { get; set; }
}

public enum DriftType
{
    Missing,      // Property exists in template but not in Azure
    Extra,        // Property exists in Azure but not in template
    Modified,     // Property value differs between template and Azure
    Added         // Resource or property was manually added in Azure (not in template)
}

public class AzureResource
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string DeploymentName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
}