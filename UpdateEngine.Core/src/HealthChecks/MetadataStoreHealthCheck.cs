// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.PackageGraph.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check for the metadata store connectivity and accessibility.
/// Tags: storage, critical
/// </summary>
public class MetadataStoreHealthCheck : IHealthCheck
{
    private readonly IMetadataStore? metadataStore;

    public MetadataStoreHealthCheck(IMetadataStore? metadataStore)
    {
        this.metadataStore = metadataStore;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (this.metadataStore == null)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Metadata store is not configured",
                        data: new Dictionary<string, object>
                        {
                            ["StoreType"] = "None"
                        }));
            }

            // Check if reindexing is required (warning state)
            var requiresReindex = this.metadataStore.IsReindexingRequired;
            if (requiresReindex)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded(
                        "Metadata store requires reindexing",
                        data: new Dictionary<string, object>
                        {
                            ["StoreType"] = this.metadataStore.GetType().Name,
                            ["RequiresReindex"] = requiresReindex
                        }));
            }

            // All checks passed
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Metadata store is accessible and healthy",
                    data: new Dictionary<string, object>
                    {
                        ["StoreType"] = this.metadataStore.GetType().Name,
                        ["RequiresReindex"] = requiresReindex
                    }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    $"Metadata store health check failed: {ex.Message}",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["StoreType"] = this.metadataStore?.GetType().Name ?? "Unknown",
                        ["Error"] = ex.Message
                    }));
        }
    }
}
