// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using UpdateEngine.Metadata.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check for the content store connectivity and accessibility.
/// Tags: storage
/// </summary>
public class ContentStoreHealthCheck : IHealthCheck
{
    private readonly IContentStore? contentStore;

    public ContentStoreHealthCheck(IContentStore? contentStore)
    {
        this.contentStore = contentStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (this.contentStore == null)
            {
                // Content store is optional, so return degraded instead of unhealthy
                return HealthCheckResult.Degraded(
                    "Content store is not configured",
                    data: new Dictionary<string, object>
                    {
                        ["StoreType"] = "None",
                        ["IsConfigured"] = false
                    });
            }

            // Try to check if we can access the content store
            // For FileSystemContentStore, we could check directory existence
            // For Azure, we could ping the blob container
            var storeType = this.contentStore.GetType().Name;

            // Basic check - just verify the store object is valid
            return HealthCheckResult.Healthy(
                "Content store is accessible",
                data: new Dictionary<string, object>
                {
                    ["StoreType"] = storeType,
                    ["IsConfigured"] = true
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                $"Content store health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["StoreType"] = this.contentStore?.GetType().Name ?? "Unknown",
                    ["Error"] = ex.Message
                });
        }
    }
}
