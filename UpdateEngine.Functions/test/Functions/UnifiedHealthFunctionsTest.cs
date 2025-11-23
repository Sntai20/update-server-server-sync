// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Storage;
using Moq;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using UpdateEngine.Functions.Management;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Services;
using Xunit;

public class UnifiedHealthFunctionsTest
{
    private readonly Mock<ILogger<UnifiedHealthFunctions>> mockLogger;
    private readonly Mock<ISyncService> mockSyncService;
    private readonly Mock<IHealthService> mockHealthService;
    private readonly Mock<IAnomalyDetectionService> mockAnomalyDetectionService;
    private readonly Mock<IMetadataStore> mockMetadataStore;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly UnifiedHealthFunctions functions;
    private readonly UnifiedHealthFunctions functionsWithAnomaly;

    public UnifiedHealthFunctionsTest()
    {
        this.mockLogger = new Mock<ILogger<UnifiedHealthFunctions>>();
        this.mockSyncService = new Mock<ISyncService>();
        this.mockHealthService = new Mock<IHealthService>();
        this.mockAnomalyDetectionService = new Mock<IAnomalyDetectionService>();
        this.mockMetadataStore = new Mock<IMetadataStore>();
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        // Functions without anomaly detection (existing tests)
        this.functions = new UnifiedHealthFunctions(
            this.mockLogger.Object,
            this.mockHealthService.Object,
            this.mockSyncService.Object,
            this.jsonOptions);

        // Functions with anomaly detection (new tests)
        this.functionsWithAnomaly = new UnifiedHealthFunctions(
            this.mockLogger.Object,
            this.mockHealthService.Object,
            this.mockSyncService.Object,
            this.jsonOptions,
            this.mockAnomalyDetectionService.Object,
            this.mockMetadataStore.Object);
    }

    [Fact]
    public async Task UniversalHealth_WithBasicScope_ShouldReturnBasicHealth()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        mockRequest.Setup(r => r.Url).Returns(new Uri("http://localhost/api/UniversalHealth?scope=basic"));
        var queryCollection = new NameValueCollection { { "scope", "basic" } };
        mockRequest.Setup(r => r.Query).Returns(queryCollection);

        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        // Act
        var result = await this.functions.UniversalHealth(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task UniversalHealth_WithFullScope_ShouldCallSystemHealth()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        mockRequest.Setup(r => r.Url).Returns(new Uri("http://localhost/api/UniversalHealth?scope=full"));
        var queryCollection = new NameValueCollection { { "scope", "full" } };
        mockRequest.Setup(r => r.Query).Returns(queryCollection);

        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        var healthStatus = new HealthStatus { IsHealthy = true };
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        var result = await this.functions.UniversalHealth(mockRequest.Object);

        // Assert
        this.mockHealthService.Verify(x => x.GetSystemHealthAsync(), Times.Once);
    }

    [Fact]
    public async Task UniversalHealth_WithSyncScope_ShouldCallSyncHealth()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        mockRequest.Setup(r => r.Url).Returns(new Uri("http://localhost/api/UniversalHealth?scope=sync"));
        var queryCollection = new NameValueCollection { { "scope", "sync" } };
        mockRequest.Setup(r => r.Query).Returns(queryCollection);

        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        var healthStatus = new HealthStatus { IsHealthy = true };
        this.mockHealthService.Setup(x => x.GetSyncHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        var result = await this.functions.UniversalHealth(mockRequest.Object);

        // Assert
        this.mockHealthService.Verify(x => x.GetSyncHealthAsync(), Times.Once);
    }

    [Fact]
    public async Task UniversalHealth_WithStoreScope_ShouldCheckReindexing()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        mockRequest.Setup(r => r.Url).Returns(new Uri("http://localhost/api/UniversalHealth?scope=store"));
        var queryCollection = new NameValueCollection { { "scope", "store" } };
        mockRequest.Setup(r => r.Query).Returns(queryCollection);

        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        this.mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);

        // Act
        var result = await this.functions.UniversalHealth(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this.mockSyncService.Verify(x => x.IsReindexingRequired(), Times.Once);
    }

    [Fact]
    public async Task CheckReindexRequired_ShouldReturnReindexStatus()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        this.mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);

        // Act
        var result = await this.functions.CheckReindexRequired(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this.mockSyncService.Verify(x => x.IsReindexingRequired(), Times.Once);
    }

    [Fact]
    public async Task ScheduledHealthCheck_TimerTrigger_ShouldPerformHealthCheck()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var healthStatus = new HealthStatus 
        { 
            IsHealthy = true,
            Metrics = new List<HealthMetric>
            {
                new() { Name = "test", Value = "value" }
            }
        };
        
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        await this.functions.ScheduledHealthCheck(timer);

        // Assert
        this.mockHealthService.Verify(x => x.GetSystemHealthAsync(), Times.Once);
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System health check passed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledHealthCheck_WithUnhealthySystem_ShouldLogWarning()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var healthStatus = new HealthStatus 
        { 
            IsHealthy = false,
            Issues = new List<HealthIssue>
            {
                new() { Component = "test", Message = "Test issue", Severity = "Error" }
            }
        };
        
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        await this.functions.ScheduledHealthCheck(timer);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System health issues detected")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledHealthCheck_OnError_ShouldThrowException()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            this.functions.ScheduledHealthCheck(timer));
    }

    [Fact]
    public async Task WeeklyMaintenance_TimerTrigger_ShouldPerformMaintenanceOperations()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };
        
        this.mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        await this.functions.WeeklyMaintenance(timer);

        // Assert
        this.mockSyncService.Verify(x => x.IsReindexingRequired(), Times.Once);
        this.mockHealthService.Verify(x => x.GetSystemHealthAsync(), Times.Once);
    }

    [Fact]
    public async Task WeeklyMaintenance_WithReindexingRequired_ShouldPerformReindex()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };
        
        this.mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(true);
        this.mockSyncService.Setup(x => x.ReindexStoreAsync(default)).Returns(Task.CompletedTask);
        this.mockHealthService.Setup(x => x.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        await this.functions.WeeklyMaintenance(timer);

        // Assert
        this.mockSyncService.Verify(x => x.IsReindexingRequired(), Times.Once);
        this.mockSyncService.Verify(x => x.ReindexStoreAsync(default), Times.Once);
        this.mockHealthService.Verify(x => x.GetSystemHealthAsync(), Times.Once);
    }

    [Fact]
    public async Task WeeklyMaintenance_OnError_ShouldThrowException()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        
        this.mockSyncService.Setup(x => x.IsReindexingRequired())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            this.functions.WeeklyMaintenance(timer));
    }

    [Fact]
    public async Task StoreManagement_WithReindexRequest_ShouldPerformReindex()
    {
        // Arrange
        var mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        var requestJson = JsonSerializer.Serialize(new UpdateEngine.Functions.Management.StoreManagementRequest { Reindex = true }); // Use fully qualified type
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestJson));
        mockRequest.Setup(r => r.Body).Returns(stream);

        var mockResponse = new Mock<HttpResponseData>(Mock.Of<FunctionContext>());
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        this.mockSyncService.Setup(x => x.ReindexStoreAsync(default)).Returns(Task.CompletedTask);

        // Act
        var result = await this.functions.StoreManagement(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this.mockSyncService.Verify(x => x.ReindexStoreAsync(default), Times.Once);
    }

    private TimerInfo CreateMockTimerInfo()
    {
        return new TimerInfo
        {
            ScheduleStatus = new ScheduleStatus
            {
                Last = DateTime.UtcNow.AddHours(-1),
                Next = DateTime.UtcNow.AddHours(1),
                LastUpdated = DateTime.UtcNow
            },
            IsPastDue = false
        };
    }

    #region Anomaly Detection Health Tests

    [Fact]
    public async Task ScheduledHealthCheck_WithAnomalyDetection_ShouldPerformMetadataHealthCheck()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };
        var softwareUpdate = CreateMockSoftwareUpdate();

        this.mockHealthService.Setup(h => h.GetSystemHealthAsync()).ReturnsAsync(healthStatus);
        this.mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { softwareUpdate }.AsQueryable());
        this.mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.3); // Normal score

        // Act
        await this.functionsWithAnomaly.ScheduledHealthCheck(timer);

        // Assert
        this.mockHealthService.Verify(h => h.GetSystemHealthAsync(), Times.Once);
        this.mockAnomalyDetectionService.Verify(a => a.Score(It.IsAny<SoftwareUpdate>()), Times.Once);

        // Verify metadata health check was performed
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performing metadata health anomaly check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledHealthCheck_WithHighRiskAnomalies_ShouldLogWarning()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };
        var riskySoftwareUpdate = CreateMockSoftwareUpdate();

        this.mockHealthService.Setup(h => h.GetSystemHealthAsync()).ReturnsAsync(healthStatus);
        this.mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { riskySoftwareUpdate }.AsQueryable());
        this.mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.9); // High risk score

        // Act
        await this.functionsWithAnomaly.ScheduledHealthCheck(timer);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HIGH-RISK metadata anomaly detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Metadata health alert: 1 high-risk anomalies")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledHealthCheck_WithoutAnomalyServices_ShouldStillWork()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };

        this.mockHealthService.Setup(h => h.GetSystemHealthAsync()).ReturnsAsync(healthStatus);

        // Act
        await this.functions.ScheduledHealthCheck(timer); // Using functions without anomaly detection

        // Assert
        this.mockHealthService.Verify(h => h.GetSystemHealthAsync(), Times.Once);
        
        // Should log debug message about services not being available
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Anomaly detection service or metadata store not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MetadataHealthAnomalyCheck_WithNoUpdates_ShouldLogNoUpdatesFound()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };

        this.mockHealthService.Setup(h => h.GetSystemHealthAsync()).ReturnsAsync(healthStatus);
        this.mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(Array.Empty<SoftwareUpdate>().AsQueryable());

        // Act
        await this.functionsWithAnomaly.ScheduledHealthCheck(timer);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No software updates found in metadata store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MetadataHealthAnomalyCheck_WithScoringException_ShouldLogWarning()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var healthStatus = new HealthStatus { IsHealthy = true };
        var softwareUpdate = CreateMockSoftwareUpdate();

        this.mockHealthService.Setup(h => h.GetSystemHealthAsync()).ReturnsAsync(healthStatus);
        this.mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { softwareUpdate }.AsQueryable());
        this.mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Throws(new InvalidOperationException("Test scoring error"));

        // Act
        await this.functionsWithAnomaly.ScheduledHealthCheck(timer);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error analyzing update") && v.ToString()!.Contains("during health check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    private SoftwareUpdate CreateMockSoftwareUpdate()
    {
        var mockIdentity = new Mock<UpdateEngine.Metadata.Metadata.MicrosoftUpdatePackageIdentity>();
        mockIdentity.Setup(i => i.ID).Returns(Guid.NewGuid());
        
        var mockUpdate = new Mock<SoftwareUpdate>();
        mockUpdate.Setup(u => u.Id).Returns(mockIdentity.Object);
        mockUpdate.Setup(u => u.Title).Returns("Test Update for Health Check");
        
        return mockUpdate.Object;
    }
}
