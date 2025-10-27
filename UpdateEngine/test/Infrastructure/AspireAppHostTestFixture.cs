// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Infrastructure;

using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Advanced Aspire test fixture using Aspire.Hosting.Testing with IntegrationTest configuration.
/// Implements proper health checking with exponential backoff retry logic.
/// </summary>
public class AspireAppHostTestFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public HttpClient HttpClient => this._httpClient ?? throw new InvalidOperationException("Test fixture not initialized");

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

        // Add IntegrationTest configuration
        appHost.Configuration.AddJsonFile(
            path: "appsettings.IntegrationTest.json",
            optional: false,
            reloadOnChange: false);

        // Configure HTTP client with longer timeout for actual test usage
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            });
            clientBuilder.AddStandardResilienceHandler();
        });

        this._app = await appHost.BuildAsync();
        await this._app.StartAsync();

        // Aspire creates an HttpClient that knows the correct address
        this._httpClient = this._app.CreateHttpClient("UpdateEngine");

        Console.WriteLine($"UpdateEngine HttpClient created successfully");
        Console.WriteLine($"Base address: {this._httpClient.BaseAddress}");

        // Wait for Functions to be fully initialized with health checks
        await this.WaitForFunctionsHealthyAsync();
    }

    /// <summary>
    /// Waits for Azure Functions to be healthy by polling a simple endpoint with exponential backoff.
    /// Uses a dedicated HttpClient without Polly resilience policies to avoid timeout conflicts.
    /// </summary>
    private async Task WaitForFunctionsHealthyAsync()
    {
        const int maxAttempts = 20; // 20 attempts with exponential backoff = ~3 minutes max
        const int initialDelayMs = 5000; // Start with 5 seconds (give Functions time to start)
        const int retryDelayMs = 3000; // 3 seconds between attempts
        const int requestTimeoutSeconds = 5; // 5 second timeout per health check request

        Console.WriteLine("Waiting for Azure Functions to become healthy...");
        Console.WriteLine($"Initial delay: {initialDelayMs}ms to allow Functions runtime to start...");

        // Give Functions time to start the worker process and discover functions
        await Task.Delay(initialDelayMs);

        // Create a simple HttpClient without Polly resilience policies for health checks
        using var healthCheckClient = new HttpClient
        {
            BaseAddress = this._httpClient!.BaseAddress,
            Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds)
        };

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Console.WriteLine($"Health check attempt {attempt}/{maxAttempts}...");

                // Try to hit a simple GET endpoint (GetStoreStatus is a good health check)
                var response = await healthCheckClient.GetAsync("/api/GetStoreStatus");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✓ Azure Functions is healthy after {attempt} attempt(s)");
                    Console.WriteLine($"  Response: {content.Substring(0, Math.Min(100, content.Length))}...");

                    // Give it one more second to stabilize
                    await Task.Delay(1000);
                    return;
                }

                Console.WriteLine($"  Status: {(int)response.StatusCode} {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"  Connection failed: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"  Request timed out after {requestTimeoutSeconds} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Unexpected error: {ex.GetType().Name}: {ex.Message}");
            }

            if (attempt < maxAttempts)
            {
                Console.WriteLine($"  Waiting {retryDelayMs}ms before retry...");
                await Task.Delay(retryDelayMs);
            }
        }

        throw new TimeoutException(
            $"Azure Functions failed to become healthy after {maxAttempts} attempts over {(initialDelayMs + (maxAttempts * (requestTimeoutSeconds * 1000 + retryDelayMs))) / 1000} seconds. " +
            "Possible issues:\n" +
            "  1. Azurite may not have started in time\n" +
            "  2. Functions worker process may have crashed during storage initialization\n" +
            "  3. Functions may not have finished discovering endpoints\n" +
            "Check the test output logs for Azure Functions startup errors.");
    }

    public async Task DisposeAsync()
    {
        this._httpClient?.Dispose();

        if (this._app != null)
        {
            await this._app.StopAsync();
            await this._app.DisposeAsync();
        }
    }
}

[CollectionDefinition("AspireAppHost")]
public class AspireAppHostCollection : ICollectionFixture<AspireAppHostTestFixture>
{
}