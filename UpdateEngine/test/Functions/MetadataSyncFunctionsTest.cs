// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Functions;

using Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.Storage;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngine.Functions.Management;
using UpdateEngine.Services;
using Xunit;

/// <summary>
/// Tests for unified sync functions - updated to use UnifiedSyncFunctions.
/// </summary>
public class MetadataSyncFunctionsTest
{
    private readonly Mock<ILogger<UnifiedSyncFunctions>> loggerMock;
    private readonly Mock<ISyncService> syncServiceMock;
    private readonly Mock<IContentStore> contentStoreMock;
    private readonly Mock<IConfiguration> configurationMock;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly UnifiedSyncFunctions functions;

    public MetadataSyncFunctionsTest()
    {
        this.loggerMock = new Mock<ILogger<UnifiedSyncFunctions>>();
        this.syncServiceMock = new Mock<ISyncService>();
        this.contentStoreMock = new Mock<IContentStore>();
        this.configurationMock = new Mock<IConfiguration>();
        this.jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            WriteIndented = true 
        };

        this.functions = new UnifiedSyncFunctions(
            this.loggerMock.Object,
            this.syncServiceMock.Object,
            this.jsonOptions,
            this.configurationMock.Object,
            this.contentStoreMock.Object);
    }

    [Fact]
    public async Task UniversalSync_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new UpdateEngine.Services.UniversalSyncRequest
        {
            SyncType = "critical",
            SyncCategories = false,
            SyncUpdates = true,
            SyncContent = false
        };

        var requestJson = JsonSerializer.Serialize(request);
        var httpRequest = CreateMockHttpRequest(requestJson);

        var mockFilter = new Mock<UpstreamSourceFilter>().Object;
        this.syncServiceMock.Setup(x => x.CreateCriticalUpdatesFilter()).Returns(mockFilter);
        this.syncServiceMock.Setup(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var response = await this.functions.UniversalSync(httpRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        this.syncServiceMock.Verify(x => x.SyncUpdatesAsync(It.IsAny<UpstreamSourceFilter>(), default), Times.Once);
    }

    private HttpRequestData CreateMockHttpRequest(string body)
    {
        var context = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(context.Object);
        
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        request.Setup(r => r.Body).Returns(stream);
        
        var mockResponse = new Mock<HttpResponseData>(context.Object);
        mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
        request.Setup(r => r.CreateResponse()).Returns(mockResponse.Object);
        request.Setup(r => r.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(mockResponse.Object);
        
        return request.Object;
    }
}