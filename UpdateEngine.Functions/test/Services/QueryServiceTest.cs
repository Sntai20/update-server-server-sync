// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Services;

using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Storage;
using Moq;
using UpdateEngine.Core.Services;
using Xunit;

public class QueryServiceTest
{
    private readonly Mock<ILogger<QueryService>> loggerMock;
    private readonly Mock<IMetadataStore> metadataStoreMock;
    private readonly QueryService queryService;

    public QueryServiceTest()
    {
        this.loggerMock = new Mock<ILogger<QueryService>>();
        this.metadataStoreMock = new Mock<IMetadataStore>();
        this.queryService = new QueryService(this.loggerMock.Object, this.metadataStoreMock.Object);
    }

    [Fact]
    public async Task GetStoreStatusAsync_ShouldReturnStatusWithBasicInformation()
    {
        // Arrange
        this.metadataStoreMock.Setup(x => x.IsReindexingRequired).Returns(false);

        // Act
        var status = await this.queryService.GetStoreStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.False(status.ReindexingRequired);
        Assert.True(status.Timestamp > DateTime.MinValue);
    }

    [Fact]
    public async Task QueryMetadataAsync_WithValidRequest_ShouldReturnResults()
    {
        // Arrange
        var request = new MetadataQueryRequest
        {
            ProductFilters = new List<string> { "Windows 10" },
            MaxResults = 10
        };

        // Act
        var results = await this.queryService.QueryMetadataAsync(request);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.RequestTimestamp > DateTime.MinValue);
    }

    [Fact]
    public async Task GetAvailableFiltersAsync_ShouldReturnFilterOptions()
    {
        // Act
        var filters = await this.queryService.GetAvailableFiltersAsync();

        // Assert
        Assert.NotNull(filters);
        Assert.NotNull(filters.Products);
        Assert.NotNull(filters.Classifications);
    }
}
