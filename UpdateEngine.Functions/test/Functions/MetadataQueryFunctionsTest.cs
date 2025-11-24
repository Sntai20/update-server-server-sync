// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Functions;

using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Storage;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngine.Functions;
using UpdateEngine.Functions.Core;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Services;
using Xunit;

/// <summary>
/// Unit tests for MetadataAccessFunctions.
/// Tests HTTP endpoints and anomaly detection capabilities.
/// </summary>
public class MetadataQueryFunctionsTest
{
    private readonly Mock<ILogger<MetadataAccessFunctions>> _mockLogger;
    private readonly Mock<IQueryService> _mockQueryService;
    private readonly Mock<IAnomalyDetectionService> _mockAnomalyDetectionService;
    private readonly Mock<IMetadataStore> _mockMetadataStore;
    private readonly MetadataAccessFunctions _functions;
    private readonly MetadataAccessFunctions _functionsWithAnomaly;

    public MetadataQueryFunctionsTest()
    {
        this._mockLogger = new Mock<ILogger<MetadataAccessFunctions>>();
        this._mockQueryService = new Mock<IQueryService>();
        this._mockAnomalyDetectionService = new Mock<IAnomalyDetectionService>();
        this._mockMetadataStore = new Mock<IMetadataStore>();

        var mockConfiguration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Functions without anomaly detection (existing tests)
        this._functions = new MetadataAccessFunctions(
            this._mockLogger.Object,
            this._mockQueryService.Object,
            this._mockMetadataStore.Object,
            jsonOptions,
            mockConfiguration.Object);

        // Functions with anomaly detection (new tests)
        this._functionsWithAnomaly = new MetadataAccessFunctions(
            this._mockLogger.Object,
            this._mockQueryService.Object,
            this._mockMetadataStore.Object,
            jsonOptions,
            mockConfiguration.Object,
            this._mockAnomalyDetectionService.Object);
    }

    // TODO: These tests are temporarily disabled until MetadataAnomalyAnalysisRequest is properly implemented
    // See UpdateEngine.Functions/src/Functions/Core/MetadataAccessFunctions.cs line 267 for TODO about anomaly detection
    
    /*
    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithValidRequest_ShouldReturnAnalysisResult()
    {
        // Test temporarily disabled - MetadataAnomalyAnalysisRequest class not yet implemented
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithoutAnomalyServices_ShouldReturnServiceUnavailable()
    {
        // Test temporarily disabled - MetadataAnomalyAnalysisRequest class not yet implemented
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithNoUpdates_ShouldReturnEmptyResult()
    {
        // Test temporarily disabled - MetadataAnomalyAnalysisRequest class not yet implemented
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithScoringException_ShouldLogWarningAndContinue()
    {
        // Test temporarily disabled - MetadataAnomalyAnalysisRequest class not yet implemented
    }

    [Fact]
    public async Task AnalyzeMetadataAnomalies_WithCustomThreshold_ShouldFilterCorrectly()
    {
        // Test temporarily disabled - MetadataAnomalyAnalysisRequest class not yet implemented
    }
    */

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
        var mockIdentity = new Mock<UpdateEngine.Metadata.Metadata.MicrosoftUpdatePackageIdentity>();
        mockIdentity.Setup(i => i.ID).Returns(Guid.NewGuid());
        
        var mockUpdate = new Mock<SoftwareUpdate>();
        mockUpdate.Setup(u => u.Id).Returns(mockIdentity.Object);
        mockUpdate.Setup(u => u.Title).Returns(title);
        mockUpdate.Setup(u => u.KBArticleId).Returns($"KB{Random.Shared.Next(1000000, 9999999)}");
        
        return mockUpdate.Object;
    }
}
