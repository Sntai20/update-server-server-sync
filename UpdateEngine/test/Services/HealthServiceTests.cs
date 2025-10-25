// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Services;

using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using UpdateEngine.Services;
using Moq;
using Xunit;

public class HealthServiceTests
{
    private readonly Mock<ILogger<HealthService>> loggerMock;
    private readonly Mock<IMetadataStore> metadataStoreMock;
    private readonly Mock<IContentStore> contentStoreMock;
    private readonly HealthService healthService;

    public HealthServiceTests()
    {
        this.loggerMock = new Mock<ILogger<HealthService>>();
        this.metadataStoreMock = new Mock<IMetadataStore>();
        this.contentStoreMock = new Mock<IContentStore>();
        this.healthService = new HealthService(
            this.loggerMock.Object, 
            this.metadataStoreMock.Object, 
            this.contentStoreMock.Object);
    }

    [Fact]
    public async Task GetSystemHealthAsync_WithHealthyStores_ShouldReturnHealthy()
    {
        // Arrange
        this.metadataStoreMock.Setup(x => x.IsReindexingRequired).Returns(false);

        // Act
        var health = await this.healthService.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(health);
        Assert.True(health.IsHealthy);
        Assert.NotNull(health.Metrics);
        Assert.True(health.Timestamp > DateTime.MinValue);
    }

    [Fact]
    public async Task GetSyncHealthAsync_ShouldReturnSyncSpecificHealth()
    {
        // Act
        var health = await this.healthService.GetSyncHealthAsync();

        // Assert
        Assert.NotNull(health);
        Assert.True(health.Timestamp > DateTime.MinValue);
    }

    [Fact]
    public async Task GetSystemHealthAsync_WithNullContentStore_ShouldStillBeHealthy()
    {
        // Arrange
        var healthServiceWithoutContent = new HealthService(
            this.loggerMock.Object, 
            this.metadataStoreMock.Object, 
            null);

        // Act
        var health = await healthServiceWithoutContent.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(health);
        Assert.True(health.IsHealthy);
    }
}