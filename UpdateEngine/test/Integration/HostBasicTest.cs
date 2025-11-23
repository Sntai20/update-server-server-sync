namespace UpdateEngineTest.Integration;

using System.Net;
using UpdateEngineTest.Infrastructure;
using Xunit;

[Collection("AspireAppHost")]
public class AppHostBasicTest
{
    private readonly AspireAppHostTestFixture _fixture;

    public AppHostBasicTest(AspireAppHostTestFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task QueryMetadataStoreStatus_WithAppHost_ReturnsSuccess()
    {
        // Act - HttpClient already knows the correct base URL
        var response = await this._fixture.HttpClient.GetAsync("/api/QueryMetadataStoreStatus");

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task ClientWebService_WithAppHost_IsAccessible()
    {
        // Act
        var response = await this._fixture.HttpClient.GetAsync("/api/ClientWebService/ClientWebService.asmx");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode); // GET not allowed on SOAP endpoint
    }
}
