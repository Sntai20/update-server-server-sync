// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpdateEngine.Functions;
using UpdateEngine.Services;
using Configuration;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

public class MetadataSyncFunctionsTests
{
    private readonly Mock<ILogger<MetadataSyncFunctions>> loggerMock;
    private readonly Mock<ISyncService> syncServiceMock;
    private readonly Mock<IHealthService> healthServiceMock;
    private readonly Mock<IOptions<ServiceConfigurationMutable>> serviceConfigurationMock;
    private readonly MetadataSyncFunctions functions;

    public MetadataSyncFunctionsTests()
    {
        this.loggerMock = new Mock<ILogger<MetadataSyncFunctions>>();
        this.syncServiceMock = new Mock<ISyncService>();
        this.healthServiceMock = new Mock<IHealthService>();
        this.serviceConfigurationMock = new Mock<IOptions<ServiceConfigurationMutable>>();
        
        // Setup service configuration mock
        this.serviceConfigurationMock.Setup(x => x.Value).Returns(new ServiceConfigurationMutable
        {
            ServiceUrl = "http://test",
            ContentUrl = "http://test/content",
            MaxUpdateCount = 1000,
            SupportedCategories = new[] { "Test Category" },
            SyncConfiguration = new SyncConfigMutable(),
            StorageConfiguration = new StorageConfigMutable(),
            FeatureFlags = new FeatureConfigMutable()
        });
        
        this.functions = new MetadataSyncFunctions(
            this.loggerMock.Object,
            this.syncServiceMock.Object,
            this.healthServiceMock.Object,
            this.serviceConfigurationMock.Object);
    }

    [Fact]
    public async Task SyncMetadata_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new UpdateEngine.Services.SyncMetadataRequest
        {
            SyncCategories = true,
            SyncUpdates = true,
            FilterType = "critical"
        };

        var requestBody = JsonSerializer.Serialize(request);
        var httpRequest = CreateMockHttpRequest(requestBody);

        this.syncServiceMock.Setup(x => x.CreateCriticalUpdatesFilter())
            .Returns(new Microsoft.PackageGraph.MicrosoftUpdate.Source.UpstreamSourceFilter());

        // Act
        var response = await this.functions.SyncMetadata(httpRequest);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        this.syncServiceMock.Verify(x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
        this.syncServiceMock.Verify(x => x.SyncUpdatesAsync(It.IsAny<Microsoft.PackageGraph.MicrosoftUpdate.Source.UpstreamSourceFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSyncHealth_ShouldCallHealthService()
    {
        // Arrange
        var expectedHealth = new HealthStatus { IsHealthy = true };
        this.healthServiceMock.Setup(x => x.GetSyncHealthAsync())
            .ReturnsAsync(expectedHealth);

        var httpRequest = CreateMockHttpRequest("");

        // Act
        var response = await this.functions.GetSyncHealth(httpRequest);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        this.healthServiceMock.Verify(x => x.GetSyncHealthAsync(), Times.Once);
    }

    private static HttpRequestData CreateMockHttpRequest(string body)
    {
        var context = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(context.Object);
        
        request.Setup(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(body)));
        request.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var response = new Mock<HttpResponseData>(context.Object);
            response.SetupProperty(r => r.StatusCode);
            return response.Object;
        });

        return request.Object;
    }
}