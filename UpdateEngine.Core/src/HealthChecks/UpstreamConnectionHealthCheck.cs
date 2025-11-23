// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UpdateEngine.Configuration;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check for upstream Microsoft Update server connectivity.
/// Tags: network
/// </summary>
public class UpstreamConnectionHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly IHttpClientFactory? httpClientFactory;
    private static readonly HttpClient sharedClient = new();

    public UpstreamConnectionHealthCheck(
        IOptionsMonitor<AppConfig> config,
        IHttpClientFactory? httpClientFactory = null)
    {
        this.config = config;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var upstreamUrl = this.config.CurrentValue.ServiceConfiguration.UpstreamServerUrl;
            if (string.IsNullOrEmpty(upstreamUrl))
            {
                return HealthCheckResult.Degraded(
                    "Upstream server URL is not configured",
                    data: new Dictionary<string, object>
                    {
                        ["UpstreamUrl"] = "Not configured"
                    });
            }

            var client = this.httpClientFactory?.CreateClient() ?? sharedClient;

            // Try to reach the upstream server with a short timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync(upstreamUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Unauthorized is expected for some endpoints - still means we can reach the server
                return HealthCheckResult.Healthy(
                    "Upstream server is reachable",
                    data: new Dictionary<string, object>
                    {
                        ["UpstreamUrl"] = upstreamUrl,
                        ["StatusCode"] = (int)response.StatusCode,
                        ["ResponseTime"] = response.Headers.Date?.ToString() ?? "N/A"
                    });
            }

            return HealthCheckResult.Degraded(
                $"Upstream server returned unexpected status: {response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["UpstreamUrl"] = upstreamUrl,
                    ["StatusCode"] = (int)response.StatusCode
                });
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Upstream server health check timed out",
                data: new Dictionary<string, object>
                {
                    ["UpstreamUrl"] = this.config.CurrentValue.ServiceConfiguration.UpstreamServerUrl,
                    ["Error"] = "Timeout"
                });
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Degraded(
                $"Cannot reach upstream server: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["UpstreamUrl"] = this.config.CurrentValue.ServiceConfiguration.UpstreamServerUrl,
                    ["Error"] = ex.Message
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Upstream server health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["UpstreamUrl"] = this.config.CurrentValue.ServiceConfiguration.UpstreamServerUrl,
                    ["Error"] = ex.Message
                });
        }
    }
}
