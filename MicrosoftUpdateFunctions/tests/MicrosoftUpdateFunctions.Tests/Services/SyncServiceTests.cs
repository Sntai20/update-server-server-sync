namespace MicrosoftUpdateFunctions.Tests.Services;

using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using MicrosoftUpdateFunctions.Services;
using Moq;
using Xunit;

public class SyncServiceTests
{
    private readonly Mock<ILogger<SyncService>> loggerMock;
    private readonly Mock<IMetadataStore> metadataStoreMock;
    private readonly SyncService syncService;

    public SyncServiceTests()
    {
        this.loggerMock = new Mock<ILogger<SyncService>>();
        this.metadataStoreMock = new Mock<IMetadataStore>();
        this.syncService = new SyncService(this.loggerMock.Object, this.metadataStoreMock.Object);
    }

    [Fact]
    public async Task SyncCategoriesAsync_ShouldCallUpstreamCategoriesSource()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        await this.syncService.SyncCategoriesAsync(cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Synchronizing categories")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsReindexingRequired_ShouldReturnMetadataStoreValue()
    {
        // Arrange
        this.metadataStoreMock.Setup(x => x.IsReindexingRequired).Returns(true);

        // Act
        var result = await this.syncService.IsReindexingRequired();

        // Assert
        Assert.True(result);
        this.metadataStoreMock.Verify(x => x.IsReindexingRequired, Times.Once);
    }

    [Fact]
    public void CreateCriticalUpdatesFilter_ShouldReturnFilterWithCriticalClassifications()
    {
        // Act
        var filter = this.syncService.CreateCriticalUpdatesFilter();

        // Assert
        Assert.NotNull(filter);
        Assert.Contains("Critical Updates", filter.ClassificationFilter);
        Assert.Contains("Security Updates", filter.ClassificationFilter);
    }

    [Fact]
    public void CreateComprehensiveUpdatesFilter_ShouldReturnFilterWithAllClassifications()
    {
        // Act
        var filter = this.syncService.CreateComprehensiveUpdatesFilter();

        // Assert
        Assert.NotNull(filter);
        Assert.Contains("Updates", filter.ClassificationFilter);
        Assert.Contains("Critical Updates", filter.ClassificationFilter);
        Assert.Contains("Security Updates", filter.ClassificationFilter);
    }

    [Fact]
    public void CreateCustomFilter_ShouldCreateFilterWithProvidedValues()
    {
        // Arrange
        var products = new List<string> { "Windows 10", "Windows 11" };
        var classifications = new List<string> { "Security Updates" };

        // Act
        var filter = this.syncService.CreateCustomFilter(products, classifications);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal(products, filter.ProductFilter);
        Assert.Equal(classifications, filter.ClassificationFilter);
    }
}