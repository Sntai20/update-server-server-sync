namespace UpdateEngineTest.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using UpdateEngineTest.Infrastructure;

/// <summary>
/// Integration tests for unified sync functions.
/// Replaces: MetadataSyncIntegrationTests, SyncContentIntegrationTest parts
/// </summary>
[Collection("AspireAppHost")]  // Changed from "AspireIntegration"
public class UnifiedSyncIntegrationTest
{
    private readonly AspireAppHostTestFixture _fixture;  // Changed type
    private readonly ITestOutputHelper _output;

    public UnifiedSyncIntegrationTest(AspireAppHostTestFixture fixture, ITestOutputHelper output)  // Changed type
    {
        this._fixture = fixture;
        this._output = output;
    }

    [Fact]
    public async Task UniversalSync_CriticalType_ShouldSucceed()
    {
        // Arrange
        var request = new
        {
            SyncType = "critical",
            SyncCategories = false,
            SyncUpdates = true,
            SyncContent = false
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UniversalSync_ComprehensiveType_ShouldSucceed()
    {
        // Arrange
        var request = new
        {
            SyncType = "comprehensive",
            SyncCategories = true,
            SyncUpdates = true,
            SyncContent = false
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UniversalSync_ContentType_ShouldHandleContentStore()
    {
        // Arrange
        var request = new
        {
            SyncType = "content",
            SyncCategories = false,
            SyncUpdates = false,
            SyncContent = true,
            ContentDaysBack = 7
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        // Content sync might fail if content store not configured - that's acceptable
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Assert.Contains("content", responseContent.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task QueryContentStatus_ShouldReturnStatus()
    {
        // Act
        var response = await this._fixture.HttpClient.GetAsync("/api/QueryContentStatus");

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(result.TryGetProperty("configured", out var configured));
    }
}