// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.ML;
using Moq;
using UpdateEngine.Services;
using UpdateEngine.Models;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace UpdateEngineTest.Services;

/// <summary>
/// Unit tests for AnomalyDetectionService.
/// Tests ML.NET integration and Microsoft Update library capabilities.
/// </summary>
public class AnomalyDetectionServiceTests
{
    private readonly Mock<ILogger<AnomalyDetectionService>> _loggerMock;
    private readonly Mock<IMetadataStore> _metadataStoreMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AnomalyDetectionService _service;

    public AnomalyDetectionServiceTests()
    {
        this._loggerMock = new Mock<ILogger<AnomalyDetectionService>>();
        this._metadataStoreMock = new Mock<IMetadataStore>();
        this._configurationMock = new Mock<IConfiguration>();
        
        // Setup configuration mock
        this._configurationMock.Setup(c => c["AnomalyDetection:ModelPath"]).Returns("test-model.zip");
        this._configurationMock.Setup(c => c["AnomalyDetection:PValueThreshold"]).Returns("0.1");
        
        this._service = new AnomalyDetectionService(
            this._loggerMock.Object,
            this._metadataStoreMock.Object,
            this._configurationMock.Object
        );
    }

    [Fact]
    public void Score_WithUpdateMetadata_ShouldReturnValidScore()
    {
        // Arrange
        var metadata = new UpdateMetadata
        {
            KB_ID = "KB1234567",
            Publisher = "Microsoft",
            HashMatch = true,
            IsSigned = true,
            FileSize = 1024000,
            DomainReputation = "Trusted",
            SupersededCount = 2,
            SupersededByCount = 0,
            BundledUpdatesCount = 1,
            IsSecurityUpdate = true,
            IsCriticalUpdate = false,
            IsCumulativeUpdate = true,
            ApplicabilityRulesCount = 5,
            HasComplexApplicability = false
        };

        // Act
        var score = this._service.Score(metadata);

        // Assert
        score.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Score_WithSuspiciousMetadata_ShouldReturnHighScore()
    {
        // Arrange - Create suspicious update metadata
        var metadata = new UpdateMetadata
        {
            KB_ID = "UNKNOWN_KB",
            Publisher = "UnknownPublisher",
            HashMatch = false,           // Suspicious
            IsSigned = false,           // Suspicious
            FileSize = 999999999,       // Very large file
            DomainReputation = "Suspicious",
            SupersededCount = 0,
            SupersededByCount = 10,     // Heavily superseded (old/bad)
            BundledUpdatesCount = 0,
            IsSecurityUpdate = false,
            IsCriticalUpdate = false,
            IsCumulativeUpdate = false,
            ApplicabilityRulesCount = 0,
            HasComplexApplicability = true  // Complex but no rules (suspicious)
        };

        // Act
        var score = this._service.Score(metadata);

        // Assert
        score.Should().BeGreaterThan(0.5, "because the metadata contains multiple suspicious indicators");
    }

    [Fact]
    public void Score_WithNormalMetadata_ShouldReturnLowScore()
    {
        // Arrange - Create normal Microsoft update metadata
        var metadata = new UpdateMetadata
        {
            KB_ID = "KB5035857",
            Publisher = "Microsoft",
            HashMatch = true,
            IsSigned = true,
            FileSize = 50000000,        // Normal size
            DomainReputation = "Trusted",
            SupersededCount = 1,        // Normal supersedence
            SupersededByCount = 0,      // Current update
            BundledUpdatesCount = 2,    // Normal bundling
            IsSecurityUpdate = true,
            IsCriticalUpdate = true,
            IsCumulativeUpdate = true,
            ApplicabilityRulesCount = 8,
            HasComplexApplicability = false
        };

        // Act
        var score = this._service.Score(metadata);

        // Assert
        score.Should().BeLessThan(0.5, "because the metadata appears normal and trustworthy");
    }

    [Fact]
    public void Score_WithSoftwareUpdate_ShouldReturnValidScore()
    {
        // Arrange
        var mockUpdate = CreateMockSoftwareUpdate();

        // Setup categories lookup for the service
        var mockCategories = new List<MicrosoftUpdatePackage>().AsQueryable();
        this._metadataStoreMock
            .Setup(m => m.OfType<MicrosoftUpdatePackage>())
            .Returns(mockCategories);

        // Act
        var score = this._service.Score(mockUpdate);

        // Assert
        score.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task DetectAnomalyAsync_WithUpdateMetadata_ShouldReturnDetectionResult()
    {
        // Arrange
        var metadata = new UpdateMetadata
        {
            KB_ID = "KB1234567",
            Publisher = "Microsoft",
            HashMatch = true,
            IsSigned = true,
            FileSize = 1024000,
            DomainReputation = "Trusted",
            SupersededCount = 2,
            SupersededByCount = 0,
            BundledUpdatesCount = 1,
            IsSecurityUpdate = true,
            IsCriticalUpdate = false,
            IsCumulativeUpdate = true,
            ApplicabilityRulesCount = 5,
            HasComplexApplicability = false
        };

        // Act
        var result = await this._service.DetectAnomalyAsync(metadata);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeInRange(0.0, 1.0);
        result.Message.Should().NotBeNullOrEmpty();
        result.IsAnomaly.Should().Be(result.Score > 0.5);
    }

    [Fact]
    public async Task DetectAnomalyAsync_WithHighAnomalyScore_ShouldMarkAsAnomaly()
    {
        // Arrange - Create highly suspicious metadata
        var metadata = new UpdateMetadata
        {
            KB_ID = "FAKE_UPDATE",
            Publisher = "Unknown",
            HashMatch = false,
            IsSigned = false,
            FileSize = 999999999,
            DomainReputation = "Malicious",
            SupersededCount = 0,
            SupersededByCount = 20,
            BundledUpdatesCount = 0,
            IsSecurityUpdate = false,
            IsCriticalUpdate = false,
            IsCumulativeUpdate = false,
            ApplicabilityRulesCount = 0,
            HasComplexApplicability = true
        };

        // Act
        var result = await this._service.DetectAnomalyAsync(metadata);

        // Assert
        result.IsAnomaly.Should().BeTrue("because the metadata is highly suspicious");
        result.Score.Should().BeGreaterThan(0.5);
        result.Message.Should().ContainEquivalentOf("anomaly");
    }

    [Fact]
    public async Task TrainModelAsync_WithTrainingData_ShouldCompleteWithoutError()
    {
        // Arrange
        var trainingData = new[]
        {
            new UpdateMetadata { KB_ID = "KB1", Publisher = "Microsoft", HashMatch = true, IsSigned = true, FileSize = 1000000, DomainReputation = "Trusted" },
            new UpdateMetadata { KB_ID = "KB2", Publisher = "Microsoft", HashMatch = true, IsSigned = true, FileSize = 2000000, DomainReputation = "Trusted" },
            new UpdateMetadata { KB_ID = "KB3", Publisher = "Unknown", HashMatch = false, IsSigned = false, FileSize = 99999999, DomainReputation = "Suspicious" }
        };

        // Act & Assert
        await this._service.TrainModelAsync(trainingData);
        // If no exception is thrown, the test passes
    }

    [Fact]
    public void IsModelReady_AfterConstruction_ShouldReturnTrue()
    {
        // Act & Assert
        this._service.IsModelReady.Should().BeTrue("because the model should be initialized during construction");
    }

    private SoftwareUpdate CreateMockSoftwareUpdate()
    {
        var mockIdentity = new Mock<Microsoft.PackageGraph.MicrosoftUpdate.Metadata.MicrosoftUpdatePackageIdentity>();
        mockIdentity.Setup(i => i.ID).Returns(Guid.NewGuid());
        
        var mockUpdate = new Mock<SoftwareUpdate>();
        mockUpdate.Setup(u => u.Id).Returns(mockIdentity.Object);
        mockUpdate.Setup(u => u.Title).Returns("Test Security Update for Windows");
        mockUpdate.Setup(u => u.KBArticleId).Returns("KB5035857");
        mockUpdate.Setup(u => u.SupersededUpdates).Returns(new List<Guid>());
        mockUpdate.Setup(u => u.IsSupersededBy).Returns((IReadOnlyList<IPackageIdentity>)new List<IPackageIdentity>());
        mockUpdate.Setup(u => u.BundledUpdates).Returns(new List<MicrosoftUpdatePackageIdentity>());
        mockUpdate.Setup(u => u.Categories).Returns(new List<Guid>());
        
        return mockUpdate.Object;
    }
}