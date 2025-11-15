using AzureDriftDetector.Services;
using AzureDriftDetector.Models;
using Newtonsoft.Json.Linq;

namespace AzureDriftDetector.Tests;

public class ComparisonServiceTests
{
    [Fact]
    public void Constructor_WithDriftIgnoreService_ShouldNotThrow()
    {
        // Arrange & Act
        var service = new ComparisonService(new DriftIgnoreService());

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullDriftIgnoreService_ShouldUseDefault()
    {
        // Arrange & Act
        var service = new ComparisonService(null);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void CompareResources_WithEmptyTemplate_ShouldReturnNoDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var emptyTemplate = JObject.Parse(@"{""resources"":[]}");
        var emptyResources = new List<AzureResource>();

        // Act
        var result = service.CompareResources(emptyTemplate, emptyResources);

        // Assert
        Assert.False(result.HasDrift);
        Assert.Empty(result.ResourceDrifts);
    }
}