using AzureDriftDetector.Services;
using AzureDriftDetector.Models;

namespace AzureDriftDetector.Tests;

public class DriftIgnoreServiceTests
{
    [Fact]
    public void Constructor_WithNonExistentFile_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        var service = new DriftIgnoreService("non_existent_file.json");
        Assert.NotNull(service);
    }

    [Fact]
    public void FilterIgnoredDrifts_WithNoDrift_ShouldReturnNoDrift()
    {
        // Arrange
        var service = new DriftIgnoreService();
        var originalResult = new DriftDetectionResult
        {
            DetectedAt = DateTime.UtcNow,
            HasDrift = false,
            Summary = "No drift"
        };

        // Act
        var filteredResult = service.FilterIgnoredDrifts(originalResult);

        // Assert
        Assert.False(filteredResult.HasDrift);
        Assert.Empty(filteredResult.ResourceDrifts);
    }

    [Fact]
    public void FilterIgnoredDrifts_WithDrift_ShouldProcessCorrectly()
    {
        // Arrange
        var service = new DriftIgnoreService();
        var originalResult = new DriftDetectionResult
        {
            DetectedAt = DateTime.UtcNow,
            HasDrift = true,
            Summary = "Has drift",
            ResourceDrifts = new List<ResourceDrift>
            {
                new ResourceDrift
                {
                    ResourceType = "Microsoft.Storage/storageAccounts",
                    ResourceName = "teststorage",
                    PropertyDrifts = new List<PropertyDrift>
                    {
                        new PropertyDrift
                        {
                            PropertyPath = "properties.accessTier",
                            ExpectedValue = "Hot",
                            ActualValue = "Cool"
                        }
                    }
                }
            }
        };

        // Act
        var filteredResult = service.FilterIgnoredDrifts(originalResult);

        // Assert
        Assert.NotNull(filteredResult);
        // Since no ignore patterns match, drift should remain
        Assert.True(filteredResult.HasDrift);
        Assert.Single(filteredResult.ResourceDrifts);
    }
}
