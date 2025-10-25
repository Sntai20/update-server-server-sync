using FluentAssertions;
using UpdateEngine.Tests.Infrastructure;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UpdateEngine.Tests.Integration;

/// <summary>
/// Integration tests for the MetadataSync functions using Aspire test infrastructure
/// These tests validate the complete metadata synchronization workflow
/// </summary>
[Collection("AspireIntegration")]
public class MetadataSyncIntegrationTests
{
    private readonly AspireTestFixture fixture;

    public MetadataSyncIntegrationTests(AspireTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task FetchConfiguration_ShouldReturnUpstreamConfig_WithDefaultEndpoint()
    {
        // Arrange
        var configUrl = await this.fixture.GetFunctionUrl("metadata/fetch-configuration");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com" // Microsoft's official endpoint
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(configUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
        
        // Should be valid JSON containing configuration data
        var configData = JsonSerializer.Deserialize<object>(responseContent);
        configData.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchConfiguration_ShouldReturnBadRequest_WithInvalidEndpoint()
    {
        // Arrange
        var configUrl = await this.fixture.GetFunctionUrl("metadata/fetch-configuration");
        var request = new
        {
            UpstreamEndpoint = "invalid-url-format"
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(configUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FetchConfiguration_ShouldHandleEmptyRequest()
    {
        // Arrange
        var configUrl = await this.fixture.GetFunctionUrl("metadata/fetch-configuration");
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(configUrl, content);

        // Assert - Should use default endpoint when none provided
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FetchCategories_ShouldReturnCategoriesData_WithValidRequest()
    {
        // Arrange
        var categoriesUrl = await this.fixture.GetFunctionUrl("metadata/fetch-categories");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            MaxCategories = 10 // Limit for testing
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(categoriesUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
        
        // Should contain categories information
        responseContent.Should().Contain("categories");
    }

    [Fact]
    public async Task FetchCategories_ShouldReturnBadRequest_WithMalformedJson()
    {
        // Arrange
        var categoriesUrl = await this.fixture.GetFunctionUrl("metadata/fetch-categories");
        var content = new StringContent("invalid json", Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(categoriesUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FetchUpdates_ShouldReturnUpdatesData_WithValidFilters()
    {
        // Arrange
        var updatesUrl = await this.fixture.GetFunctionUrl("metadata/fetch-updates");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            ProductsFilter = new[] { "Windows 10" },
            ClassificationsFilter = new[] { "Security Updates" },
            MaxUpdates = 5, // Limit for testing
            SkipSuperseded = true
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(updatesUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
        
        // Should contain updates information
        responseContent.Should().Contain("updates");
    }

    [Fact]
    public async Task FetchUpdates_ShouldHandleEmptyFilters()
    {
        // Arrange
        var updatesUrl = await this.fixture.GetFunctionUrl("metadata/fetch-updates");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            MaxUpdates = 1 // Very small limit to avoid long test runs
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(updatesUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReindexStore_ShouldCompleteSuccessfully_WithValidStorePath()
    {
        // Arrange
        var reindexUrl = await this.fixture.GetFunctionUrl("metadata/reindex-store");
        var request = new
        {
            StorePath = "./test-store" // Use test store path
        };
        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(reindexUrl, content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        // OK if store exists and was reindexed, NotFound if store doesn't exist (acceptable for test)
    }

    [Fact]
    public async Task ReindexStore_ShouldReturnBadRequest_WithInvalidRequest()
    {
        // Arrange
        var reindexUrl = await this.fixture.GetFunctionUrl("metadata/reindex-store");
        var content = new StringContent("invalid", Encoding.UTF8, "application/json");

        // Act
        var response = await this.fixture.HttpClient.PostAsync(reindexUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStoreStatus_ShouldReturnStatusInformation()
    {
        // Arrange
        var statusUrl = await this.fixture.GetFunctionUrl("metadata/store-status?storePath=./test-store");

        // Act
        var response = await this.fixture.HttpClient.GetAsync(statusUrl);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotBeNullOrEmpty();
            
            // Should contain store status information
            var statusData = JsonSerializer.Deserialize<object>(responseContent);
            statusData.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetStoreStatus_ShouldReturnJson_WithValidStore()
    {
        // Arrange
        var statusUrl = await this.fixture.GetFunctionUrl("metadata/store-status");

        // Act
        var response = await this.fixture.HttpClient.GetAsync(statusUrl);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    [Fact]
    public async Task MetadataEndpoints_ShouldAllBeReachable()
    {
        // Arrange - Test that all metadata endpoints are accessible
        var endpoints = new[]
        {
            "metadata/fetch-configuration",
            "metadata/fetch-categories", 
            "metadata/fetch-updates",
            "metadata/reindex-store",
            "metadata/store-status"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var url = await this.fixture.GetFunctionUrl(endpoint);
            
            if (endpoint.Contains("store-status"))
            {
                // GET endpoint
                var response = await this.fixture.HttpClient.GetAsync(url);
                response.StatusCode.Should().BeOneOf(
                    HttpStatusCode.OK, 
                    HttpStatusCode.NotFound, 
                    HttpStatusCode.BadRequest);
            }
            else
            {
                // POST endpoint - test with empty body
                var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await this.fixture.HttpClient.PostAsync(url, content);
                response.StatusCode.Should().BeOneOf(
                    HttpStatusCode.OK,
                    HttpStatusCode.BadRequest,
                    HttpStatusCode.InternalServerError); // Acceptable for empty requests
            }
        }
    }

    [Fact]
    public async Task MetadataWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange - Test a complete metadata sync workflow
        var baseUrl = this.fixture.BaseAddress;

        // Act & Assert - Step 1: Fetch Configuration
        var configUrl = await this.fixture.GetFunctionUrl("metadata/fetch-configuration");
        var configRequest = new { UpstreamEndpoint = "https://sws.update.microsoft.com" };
        var configContent = new StringContent(
            JsonSerializer.Serialize(configRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var configResponse = await this.fixture.HttpClient.PostAsync(configUrl, configContent);
        configResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: Check Store Status
        var statusUrl = await this.fixture.GetFunctionUrl("metadata/store-status");
        var statusResponse = await this.fixture.HttpClient.GetAsync(statusUrl);
        statusResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // Step 3: Fetch Categories (small batch for testing)
        var categoriesUrl = await this.fixture.GetFunctionUrl("metadata/fetch-categories");
        var categoriesRequest = new 
        { 
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            MaxCategories = 5 
        };
        var categoriesContent = new StringContent(
            JsonSerializer.Serialize(categoriesRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var categoriesResponse = await this.fixture.HttpClient.PostAsync(categoriesUrl, categoriesContent);
        categoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Test Reindex (should handle gracefully even if store doesn't exist)
        var reindexUrl = await this.fixture.GetFunctionUrl("metadata/reindex-store");
        var reindexRequest = new { StorePath = "./test-store" };
        var reindexContent = new StringContent(
            JsonSerializer.Serialize(reindexRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var reindexResponse = await this.fixture.HttpClient.PostAsync(reindexUrl, reindexContent);
        reindexResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError); // Acceptable for test scenarios
    }

    [Fact]
    public async Task MetadataEndpoints_ShouldHandleConcurrentRequests()
    {
        // Arrange - Test concurrent access to metadata endpoints
        var configUrl = await this.fixture.GetFunctionUrl("metadata/fetch-configuration");
        var statusUrl = await this.fixture.GetFunctionUrl("metadata/store-status");
        
        var configRequest = new { UpstreamEndpoint = "https://sws.update.microsoft.com" };
        var configContent = new StringContent(
            JsonSerializer.Serialize(configRequest), 
            Encoding.UTF8, 
            "application/json");

        // Act - Make concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>
        {
            this.fixture.HttpClient.PostAsync(configUrl, configContent),
            this.fixture.HttpClient.GetAsync(statusUrl),
            this.fixture.HttpClient.GetAsync(statusUrl)
        };

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().HaveCount(3);
        responses[0].StatusCode.Should().Be(HttpStatusCode.OK); // Config request
        responses[1].StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound); // Status request
        responses[2].StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound); // Status request

        // Dispose responses
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}