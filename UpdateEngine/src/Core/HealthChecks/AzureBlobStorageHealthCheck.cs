// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check for Azure Blob Storage connectivity and accessibility.
/// Tags: storage, azure, critical
/// </summary>
public class AzureBlobStorageHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<AppConfig> config;

    public AzureBlobStorageHealthCheck(IOptionsMonitor<AppConfig> config)
    {
        this.config = config;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageConfig = this.config.CurrentValue.StorageConfiguration;

            // If not using Azure Storage, return degraded (not applicable)
            if (!storageConfig.UseAzureStorageForMetadata && !storageConfig.UseAzureStorageForContent)
            {
                return HealthCheckResult.Degraded(
                    "Azure Blob Storage is not configured",
                    data: new Dictionary<string, object>
                    {
                        ["UseAzureForMetadata"] = false,
                        ["UseAzureForContent"] = false
                    });
            }

            // Check connection string
            if (string.IsNullOrEmpty(storageConfig.AzureStorageConnectionString) &&
                string.IsNullOrEmpty(storageConfig.AzureStorageAccountName))
            {
                return HealthCheckResult.Unhealthy(
                    "Azure Storage connection string is not configured",
                    data: new Dictionary<string, object>
                    {
                        ["ConnectionConfigured"] = false
                    });
            }

            // Try to connect to Azure Blob Storage
            var blobServiceClient = new BlobServiceClient(storageConfig.AzureStorageConnectionString);

            // Ping the service
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var properties = await blobServiceClient.GetPropertiesAsync(cts.Token);

            var containers = new List<string>();
            if (storageConfig.UseAzureStorageForMetadata)
            {
                containers.Add(storageConfig.MetadataContainerName);
            }

            if (storageConfig.UseAzureStorageForContent)
            {
                containers.Add(storageConfig.ContentContainerName);
            }

            return HealthCheckResult.Healthy(
                "Azure Blob Storage is accessible",
                data: new Dictionary<string, object>
                {
                    ["UseAzureForMetadata"] = storageConfig.UseAzureStorageForMetadata,
                    ["UseAzureForContent"] = storageConfig.UseAzureStorageForContent,
                    ["Containers"] = string.Join(", ", containers),
                    ["ServiceVersion"] = properties.Value.DefaultServiceVersion ?? "N/A"
                });
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Azure Blob Storage health check timed out",
                data: new Dictionary<string, object>
                {
                    ["Error"] = "Timeout"
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Azure Blob Storage health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                });
        }
    }
}
