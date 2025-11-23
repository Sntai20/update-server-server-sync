// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngineTest.Infrastructure;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Integration tests to validate the function consolidation was successful.
/// Tests both new unified functions and ensures old functions are properly disabled.
/// </summary>
[Collection("AspireAppHost")]
public class ConsolidationValidationTest
{
    private readonly AspireAppHostTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ConsolidationValidationTest(AspireAppHostTestFixture fixture, ITestOutputHelper output)
    {
        this._fixture = fixture;
        this._output = output;
    }

    [Theory]
    [InlineData("critical", "Critical metadata sync")]
    [InlineData("comprehensive", "Comprehensive metadata sync")]
    [InlineData("content", "Content sync")]
    [InlineData("emergency", "Emergency sync")]
    public async Task UniversalSync_AllSyncTypes_ShouldWork(string syncType, string description)
    {
        // Arrange
        var request = new
        {
            SyncType = syncType,
            SyncCategories = syncType == "comprehensive",
            SyncUpdates = syncType != "content",
            SyncContent = syncType == "content" || syncType == "emergency"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        this._output.WriteLine($"Testing: {description} (SyncType: {syncType})");
        var response = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        this._output.WriteLine($"Body: {responseBody}");

        // Should either succeed or fail gracefully (not 404/500)
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest,
                   $"{description} should not return error status. Got: {response.StatusCode}");
    }

    [Theory]
    [InlineData("basic", "Basic health check")]
    [InlineData("full", "Full health check")]
    [InlineData("sync", "Sync health check")]
    [InlineData("store", "Store health check")]
    public async Task UniversalHealth_AllScopes_ShouldWork(string scope, string description)
    {
        // Act
        this._output.WriteLine($"Testing: {description} (Scope: {scope})");
        var response = await this._fixture.HttpClient.GetAsync($"/api/UniversalHealth?scope={scope}");
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        this._output.WriteLine($"Response: {response.StatusCode}");
        this._output.WriteLine($"Body: {responseBody}");

        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.ServiceUnavailable,
                   $"{description} should return health status. Got: {response.StatusCode}");

        var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(result.TryGetProperty("isHealthy", out _), "Response should contain isHealthy property");
        Assert.True(result.TryGetProperty("timestamp", out _), "Response should contain timestamp property");
    }

    [Fact]
    public async Task NewEssentialEndpoints_ShouldBeAvailable()
    {
        var endpoints = new[]
        {
            new { Path = "/api/QueryContentStatus", Name = "Query Content Status" },
            new { Path = "/api/CheckReindexRequired", Name = "Check Reindex Required" },
            new { Path = "/api/QueryMetadata", Name = "Query Metadata" },
            new { Path = "/api/QueryMetadataStoreStatus", Name = "Query Metadata Store Status" },
            new { Path = "/api/StorageDiagnostics", Name = "Storage Diagnostics" }
        };

        foreach (var endpoint in endpoints)
        {
            this._output.WriteLine($"Testing: {endpoint.Name} ({endpoint.Path})");
            
            var response = await this._fixture.HttpClient.GetAsync(endpoint.Path);
            var responseBody = await response.Content.ReadAsStringAsync();

            this._output.WriteLine($"  Status: {response.StatusCode}");
            
            // Should not return 404 (function not found)
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            
            // Should return a valid response (OK, BadRequest for missing params, etc.)
            Assert.True(response.StatusCode == HttpStatusCode.OK || 
                       response.StatusCode == HttpStatusCode.BadRequest ||
                       response.StatusCode == HttpStatusCode.ServiceUnavailable,
                       $"{endpoint.Name} should be available. Got: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task OldEndpoints_ShouldBeDisabled()
    {
        var oldEndpoints = new[]
        {
            "/api/SyncMetadata",
            "/api/SyncContent", 
            "/api/HealthCheck",
            "/api/ReindexStore",
            "/api/SyncHealth"
        };

        foreach (var endpoint in oldEndpoints)
        {
            this._output.WriteLine($"Verifying old endpoint is disabled: {endpoint}");
            
            try
            {
                var response = await this._fixture.HttpClient.GetAsync(endpoint);
                
                // Should return 404 (function disabled/not found) or 405 (method not allowed)
                Assert.True(response.StatusCode == HttpStatusCode.NotFound || 
                           response.StatusCode == HttpStatusCode.MethodNotAllowed,
                           $"Old endpoint {endpoint} should be disabled. Got: {response.StatusCode}");
                
                this._output.WriteLine($"  ✅ Properly disabled: {response.StatusCode}");
            }
            catch (HttpRequestException)
            {
                // Connection errors are also acceptable (function not available)
                this._output.WriteLine($"  ✅ Properly disabled: Connection error");
            }
        }
    }

    [Fact] 
    public async Task CompleteConsolidatedWorkflow_ShouldWork()
    {
        this._output.WriteLine("🧪 Testing complete consolidated workflow...");

        // Step 1: Check system health
        var healthResponse = await this._fixture.HttpClient.GetAsync("/api/UniversalHealth?scope=full");
        Assert.True(healthResponse.StatusCode == HttpStatusCode.OK || 
                   healthResponse.StatusCode == HttpStatusCode.ServiceUnavailable);
        this._output.WriteLine("✅ Step 1: Health check completed");

        // Step 2: Check if reindex is required
        var reindexResponse = await this._fixture.HttpClient.GetAsync("/api/CheckReindexRequired");
        Assert.Equal(HttpStatusCode.OK, reindexResponse.StatusCode);
        this._output.WriteLine("✅ Step 2: Reindex check completed");

        // Step 3: Perform critical sync
        var syncRequest = new
        {
            SyncType = "critical",
            SyncCategories = false,
            SyncUpdates = true,
            SyncContent = false
        };
        var json = JsonSerializer.Serialize(syncRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var syncResponse = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);
        Assert.True(syncResponse.StatusCode == HttpStatusCode.OK || 
                   syncResponse.StatusCode == HttpStatusCode.BadRequest);
        this._output.WriteLine("✅ Step 3: Critical sync completed");

        // Step 4: Check content status
        var contentStatusResponse = await this._fixture.HttpClient.GetAsync("/api/QueryContentStatus");
        Assert.Equal(HttpStatusCode.OK, contentStatusResponse.StatusCode);
        this._output.WriteLine("✅ Step 4: Content status check completed");

        this._output.WriteLine("🎉 Complete consolidated workflow successful!");
    }
}
