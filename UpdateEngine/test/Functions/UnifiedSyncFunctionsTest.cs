// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using UpdateEngine.Services;
using UpdateEngine.Models;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngine.Functions;
using Xunit;
using Configuration;

/// <summary>
/// Unit tests for UnifiedSyncFunctions.
/// Replaces tests for duplicate sync functions.
/// Tests both HTTP endpoints and timer-triggered functions.
/// </summary>
public class UnifiedSyncFunctionsTest
{
    private readonly Mock<ILogger<UnifiedSyncFunctions>> _mockLogger;
    private readonly Mock<ISyncService> _mockSyncService;
    private readonly Mock<IContentStore> _mockContentStore;
    private readonly Mock<IAnomalyDetectionService> _mockAnomalyDetectionService;
    private readonly Mock<IMetadataStore> _mockMetadataStore;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly UnifiedSyncFunctions _functions;
    private readonly UnifiedSyncFunctions _functionsWithAnomaly;

    public UnifiedSyncFunctionsTest()
    {
        this._mockLogger = new Mock<ILogger<UnifiedSyncFunctions>>();
        this._mockSyncService = new Mock<ISyncService>();
        this._mockContentStore = new Mock<IContentStore>();
        this._mockAnomalyDetectionService = new Mock<IAnomalyDetectionService>();
        this._mockMetadataStore = new Mock<IMetadataStore>();
        this._mockConfiguration = new Mock<IConfiguration>();

        // Functions without anomaly detection (existing tests)
        this._functions = new UnifiedSyncFunctions(
            this._mockLogger.Object,
            this._mockSyncService.Object,
            new JsonSerializerOptions(),
            this._mockConfiguration.Object,
            this._mockContentStore.Object);

        // Functions with anomaly detection (new tests)
        this._functionsWithAnomaly = new UnifiedSyncFunctions(
            this._mockLogger.Object,
            this._mockSyncService.Object,
            new JsonSerializerOptions(),
            this._mockConfiguration.Object,
            this._mockContentStore.Object,
            this._mockAnomalyDetectionService.Object,
            this._mockMetadataStore.Object);
    }

    [Fact]
    public async Task UniversalSync_CriticalType_ShouldCallCriticalFilter()
    {
        // Arrange
        var request = new UniversalSyncRequest
        {
            SyncType = "critical",
            SyncUpdates = true,
            SyncCategories = false,
            SyncContent = false
        };

        var mockHttpRequest = this.CreateMockHttpRequest(request);
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.CreateCriticalUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await this._functions.UniversalSync(mockHttpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this._mockSyncService.Verify(x => x.CreateCriticalUpdatesFilter(), Times.Once);
        this._mockSyncService.Verify(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default), Times.Once);
    }

    [Fact]
    public async Task UniversalSync_ComprehensiveType_ShouldCallComprehensiveFilter()
    {
        // Arrange
        var request = new UniversalSyncRequest
        {
            SyncType = "comprehensive",
            SyncUpdates = true,
            SyncCategories = true,
            SyncContent = false
        };

        var mockHttpRequest = this.CreateMockHttpRequest(request);
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.CreateComprehensiveUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncCategoriesAsync(default)).Returns(Task.CompletedTask);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);
        this._mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);

        // Act
        var result = await this._functions.UniversalSync(mockHttpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this._mockSyncService.Verify(x => x.SyncCategoriesAsync(default), Times.Once);
        this._mockSyncService.Verify(x => x.CreateComprehensiveUpdatesFilter(), Times.Once);
    }

    [Fact]
    public async Task UniversalSync_ContentType_ShouldCallContentSync()
    {
        // Arrange
        var request = new UniversalSyncRequest
        {
            SyncType = "content",
            SyncUpdates = false,
            SyncCategories = false,
            SyncContent = true,
            ContentDaysBack = 30
        };

        var mockHttpRequest = this.CreateMockHttpRequest(request);
        
        this._mockSyncService.Setup(x => x.SyncContentAsync(It.IsAny<ServiceMetadataFilter>(), It.IsAny<IContentStore>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await this._functions.UniversalSync(mockHttpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        this._mockSyncService.Verify(x => x.SyncContentAsync(It.IsAny<ServiceMetadataFilter>(), It.IsAny<IContentStore>(), default), Times.Once);
    }

    [Fact]
    public async Task SyncComprehensive_TimerTrigger_ShouldPerformComprehensiveSync()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);
        this._mockSyncService.Setup(x => x.SyncCategoriesAsync(default)).Returns(Task.CompletedTask);
        this._mockSyncService.Setup(x => x.CreateComprehensiveUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await this._functions.SyncComprehensive(timer);

        // Assert
        this._mockSyncService.Verify(x => x.SyncCategoriesAsync(default), Times.Once);
        this._mockSyncService.Verify(x => x.CreateComprehensiveUpdatesFilter(), Times.Once);
        this._mockSyncService.Verify(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default), Times.Once);
    }

    [Fact]
    public async Task SyncComprehensive_WithReindexingRequired_ShouldReindexBeforeSync()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(true);
        this._mockSyncService.Setup(x => x.ReindexStoreAsync(default)).Returns(Task.CompletedTask);
        this._mockSyncService.Setup(x => x.SyncCategoriesAsync(default)).Returns(Task.CompletedTask);
        this._mockSyncService.Setup(x => x.CreateComprehensiveUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await this._functions.SyncComprehensive(timer);

        // Assert
        this._mockSyncService.Verify(x => x.IsReindexingRequired(), Times.Once);
        this._mockSyncService.Verify(x => x.ReindexStoreAsync(default), Times.Once);
        this._mockSyncService.Verify(x => x.SyncCategoriesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SyncCritical_TimerTrigger_ShouldPerformCriticalSync()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.CreateCriticalUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await this._functions.SyncCritical(timer);

        // Assert
        this._mockSyncService.Verify(x => x.CreateCriticalUpdatesFilter(), Times.Once);
        this._mockSyncService.Verify(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default), Times.Once);
        this._mockSyncService.Verify(x => x.SyncCategoriesAsync(default), Times.Never, 
            "Critical sync should not sync categories");
    }

    [Fact]
    public async Task SyncContent_TimerTrigger_WithContentStore_ShouldPerformContentSync()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        
        this._mockSyncService.Setup(x => x.SyncContentAsync(
            It.Is<ServiceMetadataFilter>(f => f.UpdatedAfter != null), 
            It.IsAny<IContentStore>(), 
            default))
            .Returns(Task.CompletedTask);

        // Act
        await this._functions.SyncContent(timer);

        // Assert
        this._mockSyncService.Verify(x => x.SyncContentAsync(
            It.Is<ServiceMetadataFilter>(f => 
                f.UpdatedAfter.HasValue && 
                f.UpdatedAfter.Value >= DateTime.UtcNow.AddDays(-31)), 
            It.IsAny<IContentStore>(), 
            default), 
            Times.Once);
    }

    [Fact]
    public async Task SyncContent_TimerTrigger_WithoutContentStore_ShouldLogWarningAndSkip()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var functionsWithoutContentStore = new UnifiedSyncFunctions(
            this._mockLogger.Object,
            this._mockSyncService.Object,
            new JsonSerializerOptions(),
            default!);

        // Act
        await functionsWithoutContentStore.SyncContent(timer);

        // Assert
        this._mockSyncService.Verify(x => x.SyncContentAsync(
            It.IsAny<ServiceMetadataFilter>(), 
            It.IsAny<IContentStore>(), 
            default), 
            Times.Never, 
            "Should not attempt content sync without content store");
        
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Content store not configured")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task SyncComprehensive_TimerTrigger_OnError_ShouldThrowException()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        
        this._mockSyncService.Setup(x => x.IsReindexingRequired()).ReturnsAsync(false);
        this._mockSyncService.Setup(x => x.SyncCategoriesAsync(default))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            this._functions.SyncComprehensive(timer));
        
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task SyncCritical_TimerTrigger_OnError_ShouldThrowException()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.CreateCriticalUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            this._functions.SyncCritical(timer));
    }

    [Fact]
    public async Task SyncContent_TimerTrigger_OnError_ShouldThrowException()
    {
        // Arrange
        var timer = this.CreateMockTimerInfo();
        
        this._mockSyncService.Setup(x => x.SyncContentAsync(
            It.IsAny<ServiceMetadataFilter>(), 
            It.IsAny<IContentStore>(), 
            default))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            this._functions.SyncContent(timer));
    }

    [Fact]
    public async Task ScheduledSyncCritical_ShouldPerformCriticalSync()
    {
        // Arrange
        var timer = new TimerInfo();
        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        
        this._mockSyncService.Setup(x => x.CreateCriticalUpdatesFilter()).Returns(mockFilter);
        this._mockSyncService.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        var request = new UniversalSyncRequest
        {
            SyncType = "critical",
            SyncUpdates = false,
            SyncCategories = false,
            SyncContent = false,
            ContentDaysBack = 1
        };
        var mockHttpRequest = this.CreateMockHttpRequest(request);

        // Act
        await this._functions.UniversalSync(mockHttpRequest);

        // Assert
        this._mockSyncService.Verify(x => x.CreateCriticalUpdatesFilter(), Times.Once);
        this._mockSyncService.Verify(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default), Times.Once);
    }

    [Fact]
    public async Task ScheduledSyncContent_WithoutContentStore_ShouldLogWarning()
    {
        // Arrange
        var timer = new TimerInfo();
        var functionsWithoutContentStore = new UnifiedSyncFunctions(
            this._mockLogger.Object,
            this._mockSyncService.Object,
            new JsonSerializerOptions(),
            default!);

        var request = new UniversalSyncRequest
        {
            SyncType = "content",
            SyncUpdates = false,
            SyncCategories = false,
            SyncContent = true,
            ContentDaysBack = 30
        };
        var mockHttpRequest = this.CreateMockHttpRequest(request);

        // Act
        await functionsWithoutContentStore.UniversalSync(mockHttpRequest);

        // Assert
        this._mockSyncService.Verify(x => x.SyncContentAsync(It.IsAny<ServiceMetadataFilter>(), It.IsAny<IContentStore>(), default), Times.Never);
    }

    private HttpRequestData CreateMockHttpRequest(UniversalSyncRequest request)
    {
        var context = new Mock<FunctionContext>();
        var mockRequest = new Mock<HttpRequestData>(context.Object);
        
        var json = JsonSerializer.Serialize(request);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        mockRequest.Setup(r => r.Body).Returns(stream);
        
        var mockResponse = new Mock<HttpResponseData>(context.Object);
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        mockRequest.Setup(r => r.CreateResponse()).Returns(mockResponse.Object);
        mockRequest.Setup(r => r.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(mockResponse.Object);
        
        return mockRequest.Object;
    }

    private TimerInfo CreateMockTimerInfo()
    {
        return new TimerInfo
        {
            ScheduleStatus = new ScheduleStatus
            {
                Last = DateTime.UtcNow.AddHours(-24),
                Next = DateTime.UtcNow.AddHours(24),
                LastUpdated = DateTime.UtcNow
            },
            IsPastDue = false
        };
    }

    #region Anomaly Detection Tests

    [Fact]
    public async Task SyncCritical_WithAnomalyDetection_ShouldPerformPostSyncAnomalyAnalysis()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var softwareUpdate = CreateMockSoftwareUpdate();
        
        this._mockSyncService
            .Setup(s => s.CreateCriticalUpdatesFilter())
            .Returns(new Mock<UpstreamSourceFilter>().Object);

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { softwareUpdate }.AsQueryable());

        this._mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.9); // High anomaly score

        // Act
        await this._functionsWithAnomaly.SyncCritical(timer);

        // Assert
        this._mockSyncService.Verify(s => s.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), It.IsAny<CancellationToken>()), Times.Once);
        this._mockAnomalyDetectionService.Verify(a => a.Score(It.IsAny<SoftwareUpdate>()), Times.AtLeastOnce);
        
        // Verify logging of anomaly detection
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performing post-sync anomaly detection")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncComprehensive_WithAnomalyDetection_ShouldLogHighRiskAnomalies()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var highRiskUpdate = CreateMockSoftwareUpdate();
        var normalUpdate = CreateMockSoftwareUpdate();
        
        this._mockSyncService.Setup(s => s.IsReindexingRequired()).ReturnsAsync(false);
        this._mockSyncService.Setup(s => s.CreateComprehensiveUpdatesFilter()).Returns(new Mock<UpstreamSourceFilter>().Object);

        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { highRiskUpdate, normalUpdate }.AsQueryable());

        this._mockAnomalyDetectionService
            .SetupSequence(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Returns(0.9) // High risk for first update
            .Returns(0.3); // Normal for second update

        // Act
        await this._functionsWithAnomaly.SyncComprehensive(timer);

        // Assert
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("POST-SYNC ANOMALY DETECTED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("1 high-risk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncContent_WithoutAnomalyServices_ShouldStillWork()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        
        // Act
        await this._functions.SyncContent(timer); // Using functions without anomaly detection

        // Assert
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Anomaly detection service or metadata store not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never); // Should not log since these services are null

        // Should still perform sync operation
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting scheduled content sync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostSyncAnomalyDetection_WithNoUpdates_ShouldLogNoUpdatesFound()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        
        this._mockSyncService.Setup(s => s.CreateCriticalUpdatesFilter()).Returns(new Mock<UpstreamSourceFilter>().Object);
        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(Array.Empty<SoftwareUpdate>().AsQueryable());

        // Act
        await this._functionsWithAnomaly.SyncCritical(timer);

        // Assert
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No software updates found in metadata store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostSyncAnomalyDetection_WithExceptionInScoring_ShouldLogWarningAndContinue()
    {
        // Arrange
        var timer = CreateMockTimerInfo();
        var softwareUpdate = CreateMockSoftwareUpdate();
        
        this._mockSyncService.Setup(s => s.CreateCriticalUpdatesFilter()).Returns(new Mock<UpstreamSourceFilter>().Object);
        this._mockMetadataStore
            .Setup(m => m.OfType<SoftwareUpdate>())
            .Returns(new[] { softwareUpdate }.AsQueryable());

        this._mockAnomalyDetectionService
            .Setup(a => a.Score(It.IsAny<SoftwareUpdate>()))
            .Throws(new InvalidOperationException("Test scoring error"));

        // Act
        await this._functionsWithAnomaly.SyncCritical(timer);

        // Assert
        this._mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error analyzing update")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Should still complete the sync operation without failing
        this._mockSyncService.Verify(s => s.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private SoftwareUpdate CreateMockSoftwareUpdate()
    {
        var mockIdentity = new Mock<Microsoft.PackageGraph.MicrosoftUpdate.Metadata.MicrosoftUpdatePackageIdentity>();
        mockIdentity.Setup(i => i.ID).Returns(Guid.NewGuid());
        
        var mockUpdate = new Mock<SoftwareUpdate>();
        mockUpdate.Setup(u => u.Id).Returns(mockIdentity.Object);
        mockUpdate.Setup(u => u.Title).Returns("Test Update");
        
        return mockUpdate.Object;
    }
}