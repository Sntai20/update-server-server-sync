// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Configuration;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check for Azure Blob Storage connectivity and accessibility.
/// Properly handles both production Azure Storage and Azurite (local emulator).
/// Tags: storage, azure, critical
/// </summary>
public class AzureBlobStorageHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly IConfiguration configuration;

    public AzureBlobStorageHealthCheck(IOptionsMonitor<AppConfig> config, IConfiguration configuration)
    {
        this.config = config;
        this.configuration = configuration;
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
                        ["UseAzureForContent"] = false,
                        ["ConfiguredForLocalStorage"] = true
                    });
            }

            // Get connection string from Aspire or configuration
            var connectionString = this.configuration.GetConnectionString("MetadataStorageConnection") 
                ?? storageConfig.AzureStorageConnectionString;

            // Check connection string
            if (string.IsNullOrEmpty(connectionString) &&
                string.IsNullOrEmpty(storageConfig.AzureStorageAccountName))
            {
                return HealthCheckResult.Unhealthy(
                    "Azure Storage connection string is not configured",
                    data: new Dictionary<string, object>
                    {
                        ["ConnectionConfigured"] = false,
                        ["AspireConnectionAvailable"] = false
                    });
            }

            // Detect if using Azurite (local emulator)
            var isAzurite = IsAzuriteConnectionString(connectionString);

            // Try to connect to Azure Blob Storage
            var blobServiceClient = new BlobServiceClient(connectionString);

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
                isAzurite 
                    ? "Azurite (local Azure Storage emulator) is accessible" 
                    : "Azure Blob Storage is accessible",
                data: new Dictionary<string, object>
                {
                    ["UseAzureForMetadata"] = storageConfig.UseAzureStorageForMetadata,
                    ["UseAzureForContent"] = storageConfig.UseAzureStorageForContent,
                    ["Containers"] = string.Join(", ", containers),
                    ["ServiceVersion"] = properties.Value.DefaultServiceVersion ?? "N/A",
                    ["IsAzurite"] = isAzurite,
                    ["ConnectionSource"] = this.configuration.GetConnectionString("MetadataStorageConnection") != null 
                        ? "Aspire" 
                        : "Configuration"
                });
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Azure Blob Storage health check timed out",
                data: new Dictionary<string, object>
                {
                    ["Error"] = "Timeout",
                    ["TimeoutSeconds"] = 10
                });
        }
        catch (Exception ex)
        {
            // For Azurite, some exceptions are expected during initial connection
            // Check if this might be an Azurite-specific scenario
            var connectionString = this.configuration.GetConnectionString("MetadataStorageConnection") 
                ?? this.config.CurrentValue.StorageConfiguration.AzureStorageConnectionString;
            
            var isAzurite = IsAzuriteConnectionString(connectionString);
            
            if (isAzurite && (ex.Message.Contains("127.0.0.1") || ex.Message.Contains("localhost")))
            {
                return HealthCheckResult.Degraded(
                    $"Azurite connection issue (may be starting up): {ex.Message}",
                    data: new Dictionary<string, object>
                    {
                        ["IsAzurite"] = true,
                        ["Error"] = ex.Message,
                        ["Note"] = "Azurite may still be initializing. Check if Azurite container is running."
                    });
            }

            return HealthCheckResult.Unhealthy(
                $"Azure Blob Storage health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["IsAzurite"] = isAzurite
                });
        }
    }

    private static bool IsAzuriteConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return false;
        }

        // Check for Azurite indicators
        return connectionString.Contains("127.0.0.1:10000") ||  // Default Azurite blob port
               connectionString.Contains("localhost:10000") ||
               connectionString.Contains("UseDevelopmentStorage=true") ||  // Azurite shorthand
               connectionString.Contains("AccountName=devstoreaccount1");  // Default Azurite account
    }
}
