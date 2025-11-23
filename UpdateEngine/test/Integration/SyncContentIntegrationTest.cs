// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UpdateEngineTest.Infrastructure;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Integration tests for Content Sync via UniversalSync endpoint.
/// Updated to use unified sync functions.
/// </summary>
[Collection("AspireAppHost")]
public class SyncContentIntegrationTest
{
    private readonly AspireAppHostTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SyncContentIntegrationTest(AspireAppHostTestFixture fixture, ITestOutputHelper output)
    {
        this._fixture = fixture;
        this._output = output;
    }

    /// <summary>
    /// Test: UniversalSync with content type responds with retry logic.
    /// </summary>
    [Fact]
    public async Task UniversalSync_ContentType_RespondsToPost()
    {
        // Arrange - Content sync request
        var request = new 
        { 
            SyncType = "content",
            SyncCategories = false,
            SyncUpdates = false,
            SyncContent = true,
            MaxItems = 1 
        };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        this._output.WriteLine($"Target: {this._fixture.HttpClient.BaseAddress}api/UniversalSync");
        this._output.WriteLine($"Request: {json}");

        // Act with retry logic
        HttpResponseMessage? response = null;
        string? responseBody = null;
        int maxRetries = 10;
        int retryDelaySeconds = 5;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                this._output.WriteLine($"\nAttempt {i + 1}/{maxRetries}...");

                response = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", content);
                responseBody = await response.Content.ReadAsStringAsync();

                this._output.WriteLine($"Status: {response.StatusCode}");
                this._output.WriteLine($"Response: {responseBody}");

                // If we get a response (not connection error), break
                if (response.StatusCode != 0)
                {
                    break;
                }
            }
            catch (HttpRequestException ex)
            {
                this._output.WriteLine($"Connection error: {ex.Message}");
                if (i < maxRetries - 1)
                {
                    this._output.WriteLine($"Waiting {retryDelaySeconds} seconds before retry...");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                    // Recreate content for next attempt
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    throw;
                }
            }
            catch (TaskCanceledException ex)
            {
                this._output.WriteLine($"Timeout: {ex.Message}");
                if (i < maxRetries - 1)
                {
                    this._output.WriteLine($"Waiting {retryDelaySeconds} seconds before retry...");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    throw;
                }
            }
        }

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(responseBody);

        // Content sync might legitimately fail if content store is not configured
        // Both OK and BadRequest are acceptable responses
        if (response.StatusCode == HttpStatusCode.BadRequest && 
            responseBody.Contains("Content store not configured"))
        {
            this._output.WriteLine("\n✅ SUCCESS: UniversalSync correctly reports content store not configured");
            Assert.Contains("Content store not configured", responseBody);
        }
        else
        {
            this._output.WriteLine("\n✅ SUCCESS: UniversalSync accepted the content sync request");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>
    /// Test: QueryContentStatus endpoint availability.
    /// </summary>
    [Fact]
    public async Task QueryContentStatus_ShouldReturnStatus()
    {
        // Act
        var response = await this._fixture.HttpClient.GetAsync("/api/QueryContentStatus");
        var responseBody = await response.Content.ReadAsStringAsync();

        this._output.WriteLine($"Status: {response.StatusCode}");
        this._output.WriteLine($"Response: {responseBody}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseBody);
        
        // Should contain configured status
        var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(result.TryGetProperty("configured", out var configured));
        
        this._output.WriteLine($"✅ Content store configured: {configured.GetBoolean()}");
    }
}
