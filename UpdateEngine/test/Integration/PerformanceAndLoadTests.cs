// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using FluentAssertions;
using UpdateEngineTest.Infrastructure;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Xunit;

/// <summary>
/// Performance and load testing scenarios for the Microsoft Update Azure Functions
/// These tests validate system performance under various load conditions typical in enterprise environments
/// </summary>
[Collection("AspireIntegration")]
public class PerformanceAndLoadTests
{
    private readonly AspireTestFixture _fixture;
    private const int CONCURRENT_CLIENTS = 10;
    private const int STRESS_TEST_REQUESTS = 50;
    private const int PERFORMANCE_TIMEOUT_MS = 30000; // 30 seconds

    public PerformanceAndLoadTests(AspireTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MetadataSync_ShouldHandleConcurrentFetchConfigurationRequests()
    {
        // Arrange
        var configUrl = await _fixture.GetFunctionUrl("metadata/fetch-configuration");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com"
        };
        var requestJson = JsonConvert.SerializeObject(request);

        var tasks = new List<Task<(HttpResponseMessage Response, long ElapsedMs)>>();
        var stopwatch = new Stopwatch();

        // Act - Create concurrent requests
        for (int i = 0; i < CONCURRENT_CLIENTS; i++)
        {
            tasks.Add(TimedRequest(async () =>
            {
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                return await _fixture.HttpClient.PostAsync(configUrl, content);
            }));
        }

        stopwatch.Start();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(CONCURRENT_CLIENTS);
        results.Should().OnlyContain(r => r.Response.StatusCode == HttpStatusCode.OK);

        var totalTime = stopwatch.ElapsedMilliseconds;
        var averageTime = results.Average(r => r.ElapsedMs);
        var maxTime = results.Max(r => r.ElapsedMs);

        // Performance assertions
        totalTime.Should().BeLessThan(PERFORMANCE_TIMEOUT_MS);
        averageTime.Should().BeLessThan(10000); // 10 seconds average
        maxTime.Should().BeLessThan(20000); // 20 seconds max

        // Cleanup
        foreach (var result in results)
        {
            result.Response.Dispose();
        }
    }

    [Fact]
    public async Task MetadataSync_ShouldHandleHighVolumeStoreStatusRequests()
    {
        // Arrange - Simulate many clients checking store status
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var concurrentRequestsPerBatch = 20;
        var numberOfBatches = 3;

        var allResults = new ConcurrentBag<(HttpResponseMessage Response, long ElapsedMs)>();
        var batchTasks = new List<Task>();

        // Act - Create multiple batches of concurrent requests
        for (int batch = 0; batch < numberOfBatches; batch++)
        {
            var batchTask = Task.Run(async () =>
            {
                var batchRequests = new List<Task<(HttpResponseMessage, long)>>();
                
                for (int i = 0; i < concurrentRequestsPerBatch; i++)
                {
                    batchRequests.Add(TimedRequest(async () =>
                        await _fixture.HttpClient.GetAsync(statusUrl)));
                }

                var batchResults = await Task.WhenAll(batchRequests);
                foreach (var result in batchResults)
                {
                    allResults.Add(result);
                }
            });

            batchTasks.Add(batchTask);
            
            // Small delay between batches to simulate realistic load
            await Task.Delay(1000);
        }

        await Task.WhenAll(batchTasks);

        // Assert
        var results = allResults.ToArray();
        results.Should().HaveCount(concurrentRequestsPerBatch * numberOfBatches);

        // All requests should succeed or return not found (acceptable for test environment)
        results.Should().OnlyContain(r => 
            r.Response.StatusCode == HttpStatusCode.OK || 
            r.Response.StatusCode == HttpStatusCode.NotFound);

        var averageResponseTime = results.Average(r => r.ElapsedMs);
        var p95ResponseTime = results.OrderBy(r => r.ElapsedMs)
                                   .Skip((int)(results.Length * 0.95))
                                   .First().ElapsedMs;

        // Performance assertions
        averageResponseTime.Should().BeLessThan(5000); // 5 seconds average
        p95ResponseTime.Should().BeLessThan(10000); // 10 seconds for 95th percentile

        // Cleanup
        foreach (var result in results)
        {
            result.Response.Dispose();
        }
    }

    [Fact]
    public async Task MixedWorkload_ShouldMaintainPerformanceUnderLoad()
    {
        // Arrange - Mixed workload simulating real enterprise usage
        var configUrl = await _fixture.GetFunctionUrl("metadata/fetch-configuration");
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var categoriesUrl = await _fixture.GetFunctionUrl("metadata/fetch-categories");
        var contentUrl = await _fixture.GetFunctionUrl("content/sha256/test123");
        var clientSoapUrl = await _fixture.GetFunctionUrl("ClientWebService/ClientWebService.asmx");

        var results = new ConcurrentBag<(string Operation, HttpResponseMessage Response, long ElapsedMs)>();
        var tasks = new List<Task>();

        // Act - Create mixed workload
        // 40% metadata operations
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(CreateMetadataWorkload("config", configUrl, results));
            tasks.Add(CreateMetadataWorkload("status", statusUrl, results));
        }

        // 30% SOAP operations (simulating Windows Update clients)
        for (int i = 0; i < 15; i++)
        {
            tasks.Add(CreateSoapWorkload("clientsync", clientSoapUrl, results));
        }

        // 20% content requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(CreateContentWorkload("content", contentUrl, results));
        }

        // 10% category fetch operations
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(CreateCategoriesWorkload("categories", categoriesUrl, results));
        }

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var allResults = results.ToArray();
        allResults.Should().HaveCount(50); // Total expected operations

        var totalTime = stopwatch.ElapsedMilliseconds;
        var averageTime = allResults.Average(r => r.ElapsedMs);

        // Performance assertions for mixed workload
        totalTime.Should().BeLessThan(60000); // 1 minute total
        averageTime.Should().BeLessThan(8000); // 8 seconds average

        // Group by operation type and validate
        var groupedResults = allResults.GroupBy(r => r.Operation).ToList();
        
        foreach (var group in groupedResults)
        {
            var groupAverage = group.Average(r => r.ElapsedMs);
            groupAverage.Should().BeLessThan(15000, $"Operation {group.Key} took too long on average");
        }

        // Cleanup
        foreach (var result in allResults)
        {
            result.Response.Dispose();
        }
    }

    [Fact]
    public async Task LongRunningOperations_ShouldNotTimeout()
    {
        // Arrange - Test operations that might take longer (like fetching updates)
        var updatesUrl = await _fixture.GetFunctionUrl("metadata/fetch-updates");
        var request = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            MaxUpdates = 50, // Larger batch
            ProductsFilter = new[] { "Windows 10", "Windows 11" },
            ClassificationsFilter = new[] { "Security Updates" }
        };
        var requestJson = JsonConvert.SerializeObject(request);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var response = await _fixture.HttpClient.PostAsync(updatesUrl, content);
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(120000); // 2 minutes max for large operation

        response.Dispose();
    }

    [Fact]
    public async Task SystemResilience_ShouldHandleFailureRecovery()
    {
        // Arrange - Test system recovery after invalid requests
        var configUrl = await _fixture.GetFunctionUrl("metadata/fetch-configuration");
        
        var validRequest = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com"
        };
        var validRequestJson = JsonConvert.SerializeObject(validRequest);

        // Act - Send invalid request followed by valid requests
        var invalidContent = new StringContent("invalid json", Encoding.UTF8, "application/json");
        var invalidResponse = await _fixture.HttpClient.PostAsync(configUrl, invalidContent);
        
        // Follow up with multiple valid requests to test recovery
        var recoveryTasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            var validContent = new StringContent(validRequestJson, Encoding.UTF8, "application/json");
            recoveryTasks.Add(_fixture.HttpClient.PostAsync(configUrl, validContent));
        }

        var recoveryResponses = await Task.WhenAll(recoveryTasks);

        // Assert
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        recoveryResponses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        // Cleanup
        invalidResponse.Dispose();
        foreach (var response in recoveryResponses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task MemoryUsage_ShouldStayWithinLimits()
    {
        // Arrange - Test memory efficiency with many small requests
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Make many requests to test memory management
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_fixture.HttpClient.GetAsync(statusUrl));
        }

        var responses = await Task.WhenAll(tasks);
        var afterRequestsMemory = GC.GetTotalMemory(false);

        // Cleanup responses
        foreach (var response in responses)
        {
            response.Dispose();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        responses.Should().HaveCount(100);
        responses.Should().OnlyContain(r => 
            r.StatusCode == HttpStatusCode.OK || 
            r.StatusCode == HttpStatusCode.NotFound);

        // Memory should not grow excessively (allowing some growth for test overhead)
        var memoryGrowth = finalMemory - initialMemory;
        memoryGrowth.Should().BeLessThan(50 * 1024 * 1024); // Less than 50MB growth
    }

    // Helper methods
    private async Task<(HttpResponseMessage Response, long ElapsedMs)> TimedRequest(Func<Task<HttpResponseMessage>> requestFunc)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await requestFunc();
        stopwatch.Stop();
        return (response, stopwatch.ElapsedMilliseconds);
    }

    private async Task CreateMetadataWorkload(string operation, string url, ConcurrentBag<(string, HttpResponseMessage, long)> results)
    {
        var result = await TimedRequest(async () =>
        {
            if (operation == "status")
            {
                return await _fixture.HttpClient.GetAsync(url);
            }
            else
            {
                var request = new { UpstreamEndpoint = "https://sws.update.microsoft.com" };
                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                return await _fixture.HttpClient.PostAsync(url, content);
            }
        });
        
        results.Add((operation, result.Response, result.ElapsedMs));
    }

    private async Task CreateSoapWorkload(string operation, string url, ConcurrentBag<(string, HttpResponseMessage, long)> results)
    {
        var result = await TimedRequest(async () =>
        {
            var soapEnvelope = """
                <?xml version="1.0" encoding="utf-8"?>
                <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                    <soap:Body>
                        <GetConfig xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                            <protocolVersion>3.0.0.0</protocolVersion>
                            <lastChange>2023-01-01T00:00:00Z</lastChange>
                        </GetConfig>
                    </soap:Body>
                </soap:Envelope>
                """;
            return await _fixture.HttpClient.PostAsync(url, new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"));
        });
        
        results.Add((operation, result.Response, result.ElapsedMs));
    }

    private async Task CreateContentWorkload(string operation, string url, ConcurrentBag<(string, HttpResponseMessage, long)> results)
    {
        var result = await TimedRequest(async () => await _fixture.HttpClient.GetAsync(url));
        results.Add((operation, result.Response, result.ElapsedMs));
    }

    private async Task CreateCategoriesWorkload(string operation, string url, ConcurrentBag<(string, HttpResponseMessage, long)> results)
    {
        var result = await TimedRequest(async () =>
        {
            var request = new 
            { 
                UpstreamEndpoint = "https://sws.update.microsoft.com",
                MaxCategories = 5 
            };
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            return await _fixture.HttpClient.PostAsync(url, content);
        });
        
        results.Add((operation, result.Response, result.ElapsedMs));
    }
}