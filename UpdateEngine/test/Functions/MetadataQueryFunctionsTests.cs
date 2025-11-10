// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngine.Functions;
using UpdateEngine.Services;
using UpdateEngine.Models;
using Xunit;
using FluentAssertions;

namespace UpdateEngineTest.Functions;

/// <summary>
/// Unit tests for MetadataQueryFunctions.
/// Tests HTTP endpoints and anomaly detection capabilities.
/// </summary>
public class MetadataQueryFunctionsTests
{
    private readonly Mock<ILogger<MetadataQueryFunctions>> _mockLogger;
    private readonly Mock<IQueryService> _mockQueryService;
    private readonly Mock<IAnomalyDetectionService> _mockAnomalyDetectionService;
    private readonly Mock<IMetadataStore> _mockMetadataStore;
    private readonly MetadataQueryFunctions _functions;
    private readonly MetadataQueryFunctions _functionsWithAnomaly;

    public MetadataQueryFunctionsTests()
    {
        this._mockLogger = new Mock<ILogger<MetadataQueryFunctions>>();
        this._mockQueryService = new Mock<IQueryService>();
        this._mockAnomalyDetectionService = new Mock<IAnomalyDetectionService>();
        this._mockMetadataStore = new Mock<IMetadataStore>();

        // Functions without anomaly detection (existing tests)
        this._functions = new MetadataQueryFunctions(
            this._mockLogger.Object,
            this._mockQueryService.Object);

        // Functions with anomaly detection (new tests)
        this._functionsWithAnomaly = new MetadataQueryFunctions(
            this._mockLogger.Object,
            this._mockQueryService.Object,
            this._mockAnomalyDetectionService.Object,
            this._mockMetadataStore.Object);
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithValidRequest_ShouldReturnAnalysisResult()
    {
        // Arrange
        var request = CreateMockHttpRequest(new MetadataAnomalyAnalysisRequest
        {
            MaxUpdates = 10,
            AnomalyThreshold = 0.6
        });

        var softwareUpdates = new[]
        {
            CreateMockSoftwareUpdate("High Risk Update", 0.9),
            CreateMockSoftwareUpdate("Medium Risk Update", 0.7),
            CreateMockSoftwareUpdate("Normal Update", 0.3)
        };

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(softwareUpdates.AsQueryable());

        this._mockAnomalyDetectionService
            .SetupSequence(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.9)
            .Returns(0.7)
            .Returns(0.3);

        // Act
        var response = await this._functionsWithAnomaly.AnalyzeMetadataAnomalies(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify analysis was performed
        this._mockAnomalyDetectionService.Verify(a => a.Score(It.IsAny<SoftwareUpdate>()), Times.Exactly(3));
        
        // Verify logging
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing 3 software updates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithoutAnomalyServices_ShouldReturnServiceUnavailable()
    {
        // Arrange
        var request = CreateMockHttpRequest(new MetadataAnomalyAnalysisRequest());

        // Act
        var response = await this._functions.AnalyzeMetadataAnomalies(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithNoUpdates_ShouldReturnEmptyResult()
    {
        // Arrange
        var request = CreateMockHttpRequest(new MetadataAnomalyAnalysisRequest());

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(Array.Empty<SoftwareUpdate>().AsQueryable());

        // Act
        var response = await this._functionsWithAnomaly.AnalyzeMetadataAnomalies(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithScoringException_ShouldLogWarningAndContinue()
    {
        // Arrange
        var request = CreateMockHttpRequest(new MetadataAnomalyAnalysisRequest());
        var softwareUpdate = CreateMockSoftwareUpdate("Test Update", 0.8);

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { softwareUpdate }.AsQueryable());

        this._mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Throws(new InvalidOperationException("Test scoring error"));

        // Act
        var response = await this._functionsWithAnomaly.AnalyzeMetadataAnomalies(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error analyzing update")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithCustomThreshold_ShouldFilterCorrectly()
    {
        // Arrange
        var request = CreateMockHttpRequest(new MetadataAnomalyAnalysisRequest
        {
            AnomalyThreshold = 0.8  // High threshold
        });

        var softwareUpdates = new[]
        {
            CreateMockSoftwareUpdate("High Risk Update", 0.9),  // Should be detected
            CreateMockSoftwareUpdate("Medium Risk Update", 0.7), // Should NOT be detected
            CreateMockSoftwareUpdate("Normal Update", 0.3)      // Should NOT be detected
        };

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(softwareUpdates.AsQueryable());

        this._mockAnomalyDetectionService
            .SetupSequence(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.9)
            .Returns(0.7)
            .Returns(0.3);

        // Act
        var response = await this._functionsWithAnomaly.AnalyzeMetadataAnomalies(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify that only 1 anomaly should be detected (score > 0.8)
        this._mockAnomalyDetectionService.Verify(a => a.Score(It.IsAny<SoftwareUpdate>()), Times.Exactly(3));
    }

    private HttpRequestData CreateMockHttpRequest(object requestBody)
    {
        var json = JsonSerializer.Serialize(requestBody);
        var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var context = new Mock<FunctionContext>();
        var mockRequest = new Mock<HttpRequestData>(context.Object);

        mockRequest.Setup(r => r.Body).Returns(bodyStream);
        
        var mockResponse = new Mock<HttpResponseData>(context.Object);
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse()).Returns(mockResponse.Object);
        mockRequest.Setup(r => r.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(mockResponse.Object);

        return mockRequest.Object;
    }

    private SoftwareUpdate CreateMockSoftwareUpdate(string title, double anomalyScore)
    {
        var mockIdentity = new Mock<Microsoft.PackageGraph.MicrosoftUpdate.Metadata.MicrosoftUpdatePackageIdentity>();
        mockIdentity.Setup(i => i.ID).Returns(Guid.NewGuid());
        
        var mockUpdate = new Mock<SoftwareUpdate>();
        mockUpdate.Setup(u => u.Id).Returns(mockIdentity.Object);
        mockUpdate.Setup(u => u.Title).Returns(title);
        mockUpdate.Setup(u => u.KBArticleId).Returns($"KB{Random.Shared.Next(1000000, 9999999)}");
        
        return mockUpdate.Object;
    }
}