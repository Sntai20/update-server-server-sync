// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using System.Collections;
using System.Net;
using System.Text.Json;
using UpdateEngine.Services;
using Azure.Storage.Blobs;

/// <summary>
/// Diagnostic functions for troubleshooting storage and configuration issues.
/// </summary>
public class DiagnosticFunctions
{
    private readonly ILogger<DiagnosticFunctions> logger;
    private readonly IConfiguration configuration;
    private readonly IMetadataStore metadataStore;
    private readonly IContentStore? contentStore;

    public DiagnosticFunctions(
        ILogger<DiagnosticFunctions> logger,
        IConfiguration configuration,
        IMetadataStore metadataStore,
        IContentStore? contentStore = null)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.metadataStore = metadataStore;
        this.contentStore = contentStore;
    }

    /// <summary>
    /// Get storage configuration and status.
    /// GET /api/StorageDiagnostics
    /// </summary>
    [Function("StorageDiagnostics")]
    public async Task<HttpResponseData> GetStorageDiagnostics(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var diagnostics = new
            {
                MetadataStore = StorageFactory.GetStoreInfo(this.metadataStore!, this.logger),
                ContentStore = StorageFactory.GetStoreInfo(this.contentStore, this.logger),
                Configuration = new
                {
                    MetadataStorageType = Environment.GetEnvironmentVariable("UseAzureStorageForMetadata"),
                    ContentStorageType = Environment.GetEnvironmentVariable("UseAzureStorageForContent"),
                    MetadataPath = Environment.GetEnvironmentVariable("MetadataStorePath"),
                    ContentPath = Environment.GetEnvironmentVariable("ContentStorePath"),
                    MetadataContainer = Environment.GetEnvironmentVariable("MetadataContainerName"),
                    ContentContainer = Environment.GetEnvironmentVariable("ContentContainerName")
                },
                Environment = new
                {
                    Runtime = Environment.Version.ToString(),
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(diagnostics);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving storage diagnostics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Test Azure Storage connectivity and list containers.
    /// GET /api/TestAzureStorage
    /// </summary>
    [Function("TestAzureStorage")]
    public async Task<HttpResponseData> TestAzureStorage(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("Testing Azure Storage connectivity");

        try
        {
            var metadataConnectionString = this.configuration.GetConnectionString("MetadataStorageConnection");
            var contentConnectionString = this.configuration.GetConnectionString("ContentStorageConnection");

            var results = new
            {
                Timestamp = DateTime.UtcNow,
                MetadataStorage = await this.TestStorageConnection(metadataConnectionString, "Metadata"),
                ContentStorage = await this.TestStorageConnection(contentConnectionString, "Content")
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error testing Azure Storage");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message, stackTrace = ex.StackTrace });
            return errorResponse;
        }
    }

    private async Task<object> TestStorageConnection(string? connectionString, string storageName)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return new { Connected = false, Reason = "No connection string configured" };
        }

        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var accountInfo = await blobServiceClient.GetAccountInfoAsync();

            // List all containers
            var containers = new List<string>();
            await foreach (var container in blobServiceClient.GetBlobContainersAsync())
            {
                containers.Add(container.Name);
            }

            return new
            {
                Connected = true,
                AccountName = blobServiceClient.AccountName,
                AccountKind = accountInfo.Value.AccountKind.ToString(),
                SkuName = accountInfo.Value.SkuName.ToString(),
                Containers = containers,
                ContainerCount = containers.Count
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to connect to {StorageName} storage", storageName);
            return new
            {
                Connected = false,
                Error = ex.Message,
                ConnectionStringPrefix = connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "..."
            };
        }
    }

    private async Task<object> GetMetadataStoreInfo(bool useAzureStorage)
    {
        try
        {
            var identities = this.metadataStore.GetPackageIdentities();

            return new
            {
                Type = useAzureStorage ? "Azure Blob Storage" : "File System",
                Initialized = true,
                PackageCount = identities.Count,
                IsReindexingRequired = this.metadataStore.IsReindexingRequired,
                IsMetadataIndexingSupported = this.metadataStore.IsMetadataIndexingSupported,
                SamplePackages = identities.Take(5).Select(p => p.ToString()).ToList()
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting metadata store info");
            return new { Initialized = false, Error = ex.Message };
        }
    }

    private async Task<object> GetContentStoreInfo(bool useAzureStorage)
    {
        if (this.contentStore == null)
        {
            return new { Configured = false };
        }

        try
        {
            return new
            {
                Type = useAzureStorage ? "Azure Blob Storage" : "File System",
                Configured = true,
                QueuedCount = this.contentStore.QueuedCount,
                QueuedSize = this.contentStore.QueuedSize,
                DownloadedSize = this.contentStore.DownloadedSize
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting content store info");
            return new { Configured = true, Error = ex.Message };
        }
    }

    /// <summary>
    /// Get all environment variables (masked for security).
    /// GET /api/EnvironmentDiagnostics
    /// </summary>
    [Function("EnvironmentDiagnostics")]
    public async Task<HttpResponseData> EnvironmentDiagnostics(
          [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("Environment diagnostics requested");

        try
        {
            var envVars = Environment.GetEnvironmentVariables();
            var maskedVars = new Dictionary<string, string>();

            foreach (var key in envVars.Keys)
            {
                var keyStr = key?.ToString() ?? "";
                var value = envVars[key]?.ToString() ?? "";

                // Mask sensitive values
                if (keyStr.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
                  keyStr.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                keyStr.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                      keyStr.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Length > 10 ? value.Substring(0, 10) + "..." : "***";
                }

                maskedVars[keyStr] = value;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                Timestamp = DateTime.UtcNow,
                EnvironmentVariables = maskedVars.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value)
            }, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting environment diagnostics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
