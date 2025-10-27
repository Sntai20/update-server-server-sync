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
/// Integration tests for SyncContent function.
/// </summary>
[Collection("AspireAppHost")]
public class SyncContentIntegrationTests
{
    private readonly AspireAppHostTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SyncContentIntegrationTests(AspireAppHostTestFixture fixture, ITestOutputHelper output)
    {
        this._fixture = fixture;
        this._output = output;
    }

    /// <summary>
    /// Test: SyncContent endpoint responds with retry logic.
    /// </summary>
    [Fact]
    public async Task SyncContent_Endpoint_RespondsToPost()
    {
        // Arrange
        var request = new { MaxItems = 1 };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        this._output.WriteLine($"Target: {this._fixture.HttpClient.BaseAddress}api/SyncContent");
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

                response = await this._fixture.HttpClient.PostAsync("/api/SyncContent", content);
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

        // Should not be 404 (endpoint exists)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);

        // Should be either 200 OK or 400 BadRequest
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 200 or 400, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Assert.Contains("Content store", responseBody);
            this._output.WriteLine("\n✅ SUCCESS: SyncContent correctly reports content store not configured");
        }
        else
        {
            this._output.WriteLine("\n✅ SUCCESS: SyncContent accepted the request");
        }
    }
}