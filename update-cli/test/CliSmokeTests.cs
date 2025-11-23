// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateCli.Tests;

using FluentAssertions;
using Microsoft.PackageGraph.Storage;
using Moq;
using UpdateCli.Commands;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;
using Xunit;

/// <summary>
/// Smoke tests for CLI orchestrator integration.
/// Validates that the orchestrator-based CLI can be constructed and basic operations work.
/// </summary>
public class CliSmokeTests
{
    [Fact]
    public void CommandHandlers_CanBeConstructed_WithOrchestrators()
    {
        // Arrange
        var mockSync = new Mock<ISyncOrchestrator>();
        var mockMetadata = new Mock<IMetadataOrchestrator>();
        var mockContent = new Mock<IContentOrchestrator>();
        var mockHealth = new Mock<IHealthService>();
        var mockStore = new Mock<IMetadataStore>();

        // Act
        var handlers = new CommandHandlers(
            mockSync.Object,
            mockMetadata.Object,
            mockContent.Object,
            mockHealth.Object,
            mockStore.Object);

        // Assert
        handlers.Should().NotBeNull();
    }

    [Fact]
    public async Task HealthCheck_WithMockedService_ReturnsSuccessfully()
    {
        // Arrange
        var mockHealth = new Mock<IHealthService>();
        mockHealth.Setup(x => x.PerformHealthCheckAsync())
            .ReturnsAsync(new HealthCheckResult { IsHealthy = true, Status = "Healthy" });

        var handlers = new CommandHandlers(
            new Mock<ISyncOrchestrator>().Object,
            new Mock<IMetadataOrchestrator>().Object,
            new Mock<IContentOrchestrator>().Object,
            mockHealth.Object,
            new Mock<IMetadataStore>().Object);

        // Act
        var result = await handlers.HandleHealthAsync();

        // Assert
        result.Should().Be(0);
        mockHealth.Verify(x => x.PerformHealthCheckAsync(), Times.Once);
    }

    [Fact]
    public async Task Statistics_WithMockedOrchestrator_ReturnsSuccessfully()
    {
        // Arrange
        var mockMetadata = new Mock<IMetadataOrchestrator>();
        mockMetadata.Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataStatistics());

        var handlers = new CommandHandlers(
            new Mock<ISyncOrchestrator>().Object,
            mockMetadata.Object,
            new Mock<IContentOrchestrator>().Object,
            new Mock<IHealthService>().Object,
            new Mock<IMetadataStore>().Object);

        // Act
        var result = await handlers.HandleStoreStatisticsAsync();

        // Assert
        result.Should().Be(0);
        mockMetadata.Verify(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContentStatus_WithMockedOrchestrator_ReturnsSuccessfully()
    {
        // Arrange
        var mockContent = new Mock<IContentOrchestrator>();
        mockContent.Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentStatistics());

        var handlers = new CommandHandlers(
            new Mock<ISyncOrchestrator>().Object,
            new Mock<IMetadataOrchestrator>().Object,
            mockContent.Object,
            new Mock<IHealthService>().Object,
            new Mock<IMetadataStore>().Object);

        // Act
        var result = await handlers.HandleContentStatusAsync();

        // Assert
        result.Should().Be(0);
        mockContent.Verify(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
