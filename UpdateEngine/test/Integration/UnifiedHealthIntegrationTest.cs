using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using UpdateEngineTest.Infrastructure;

namespace UpdateEngineTest.Integration;

/// <summary>
/// Integration tests for unified health functions.
/// Replaces health-related tests from multiple test files.
/// </summary>
[Collection("AspireAppHost")]  // Changed from "AspireIntegration"
public class UnifiedHealthIntegrationTest
{
    private readonly AspireAppHostTestFixture _fixture;  // Changed type
    private readonly ITestOutputHelper _output;

    public UnifiedHealthIntegrationTest(AspireAppHostTestFixture fixture, ITestOutputHelper output)  // Changed type
    {
        this._fixture = fixture;
        this._output = output;
    }

    [Theory]
    [InlineData("basic")]
    [InlineData("full")]
    [InlineData("sync")]
    [InlineData("store")]
    public async Task UniversalHealth_WithScope_ShouldReturnHealthStatus(string scope)
    {
        // Act
        var response = await this._fixture.HttpClient.GetAsync($"/api/UniversalHealth?scope={scope}");

        // Assert
        this._output.WriteLine($"Scope: {scope}, Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.ServiceUnavailable);

        var healthResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(healthResult.TryGetProperty("isHealthy", out var isHealthy));
        Assert.True(healthResult.TryGetProperty("timestamp", out var timestamp));
        
        if (healthResult.TryGetProperty("scope", out var resultScope))
        {
            Assert.Equal(scope, resultScope.GetString());
        }
    }

    [Fact]
    public async Task CheckReindexRequired_ShouldReturnValidResponse()
    {
        // Act
        var response = await this._fixture.HttpClient.GetAsync("/api/CheckReindexRequired");

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(result.TryGetProperty("reindexRequired", out var reindexRequired));
        Assert.True(reindexRequired.ValueKind == JsonValueKind.True || reindexRequired.ValueKind == JsonValueKind.False);
    }

    [Fact]
    public async Task UniversalHealth_DefaultScope_ShouldReturnBasicHealth()
    {
        // Act - No scope parameter = basic
        var response = await this._fixture.HttpClient.GetAsync("/api/UniversalHealth");

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        this._output.WriteLine($"Response Content: {responseContent}");

        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.ServiceUnavailable);

        var healthResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(healthResult.TryGetProperty("isHealthy", out var isHealthy));
        Assert.True(healthResult.TryGetProperty("timestamp", out var timestamp));
    }
}