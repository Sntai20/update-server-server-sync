// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Azure;
using Microsoft.PackageGraph.Storage.Local;

/// <summary>
/// Factory for creating metadata and content stores with consistent configuration.
/// </summary>
public static class StorageFactory
{
    /// <summary>
    /// Opens or creates a metadata store based on configuration.
    /// </summary>
    public static IMetadataStore CreateMetadataStore(
        string storePath,
        string storeType,
        string? connectionString,
        string? containerName,
        bool createIfNotExists,
  ILogger logger)
    {
        logger.LogInformation(
      "Creating metadata store - Type: {Type}, Path: {Path}, CreateIfNotExists: {Create}",
       storeType,
  storePath,
    createIfNotExists);

        switch (storeType.ToLowerInvariant())
        {
            case "azure":
            case "azureblob":
                return CreateAzureBlobMetadataStore(connectionString, containerName ?? storePath, createIfNotExists, logger);

            case "local":
            case "filesystem":
                return CreateLocalMetadataStore(storePath, createIfNotExists, logger);

            default:
                throw new ArgumentException($"Unsupported metadata store type: {storeType}");
        }
    }

    /// <summary>
    /// Opens or creates a content store based on configuration.
    /// </summary>
    public static IContentStore? CreateContentStore(
  string? storePath,
     string storeType,
        string? connectionString,
 string? containerName,
        bool createIfNotExists,
   ILogger logger)
    {
        if (string.IsNullOrEmpty(storePath))
        {
            logger.LogInformation("Content store path not configured - running in catalog-only mode");
            return null;
        }

        logger.LogInformation(
          "Creating content store - Type: {Type}, Path: {Path}, CreateIfNotExists: {Create}",
            storeType,
              storePath,
         createIfNotExists);

        switch (storeType.ToLowerInvariant())
        {
            case "azure":
            case "azureblob":
                return CreateAzureBlobContentStore(connectionString, containerName ?? storePath, createIfNotExists, logger);

            case "local":
            case "filesystem":
                return CreateLocalContentStore(storePath, createIfNotExists, logger);

            default:
                throw new ArgumentException($"Unsupported content store type: {storeType}");
        }
    }

    /// <summary>
    /// Validates or creates a directory for local storage.
    /// </summary>
    public static void ValidateOrCreateDirectory(string path, ILogger logger)
    {
        if (Directory.Exists(path))
        {
            logger.LogInformation("Directory already exists: {Path}", path);
            return;
        }

        logger.LogInformation("Creating directory: {Path}", path);
        Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Gets diagnostic information about a metadata store.
    /// </summary>
    public static object GetStoreInfo(IMetadataStore store, ILogger logger)
    {
        try
        {
            return new
            {
                Type = store.GetType().Name,
                IsIndexed = !store.IsReindexingRequired,
                UpdateCount = store.GetPendingPackages().Count,
                TotalPackageCount = store.GetPackageIdentities().Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get store information");
            return new { Status = "Error", Error = ex.Message };
        }
    }

    /// <summary>
    /// Gets diagnostic information about a content store.
    /// </summary>
    public static object? GetStoreInfo(IContentStore? store, ILogger logger)
    {
        if (store == null)
        {
            return new { Status = "Not configured" };
        }

        try
        {
            return new
            {
                Type = store.GetType().Name,
                Status = "Operational"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get content store information");
            return new { Status = "Error", Error = ex.Message };
        }
    }

    /// <summary>
    /// Validates storage configuration without creating stores.
    /// </summary>
    public static StorageValidationResult ValidateConfiguration(
  string storePath,
        string storeType,
  string? connectionString,
ILogger logger)
    {
        var result = new StorageValidationResult();

        try
        {
            switch (storeType.ToLowerInvariant())
            {
                case "azure":
                case "azureblob":
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Connection string is required for Azure storage";
                    }
                    else
                    {
                        // Try to parse connection string
                        _ = new BlobServiceClient(connectionString);
                        result.IsValid = true;
                    }
                    break;

                case "local":
                case "filesystem":
                    var directoryInfo = new DirectoryInfo(storePath);
                    result.IsValid = directoryInfo.Parent?.Exists ?? false;
                    if (!result.IsValid)
                    {
                        result.ErrorMessage = $"Parent directory does not exist: {directoryInfo.Parent?.FullName}";
                    }
                    break;

                default:
                    result.IsValid = false;
                    result.ErrorMessage = $"Unsupported store type: {storeType}";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            logger.LogError(ex, "Storage configuration validation failed");
        }

        return result;
    }

    private static IMetadataStore CreateAzureBlobMetadataStore(
        string? connectionString,
        string containerName,
        bool createIfNotExists,
   ILogger logger)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string required for Azure Blob metadata stores");
        }

        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (createIfNotExists)
            {
                var createResponse = containerClient.CreateIfNotExists();
                if (createResponse != null)
                {
                    logger.LogInformation("Created Azure Blob container: {ContainerName}", containerName);
                }
                else
                {
                    logger.LogInformation("Azure Blob container already exists: {ContainerName}", containerName);
                }
            }

            logger.LogInformation(
   "Successfully connected to Azure Storage account: {AccountName}",
             blobServiceClient.AccountName);

            return Microsoft.PackageGraph.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, containerName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Azure Blob Storage for metadata store");
            throw;
        }
    }

    private static IMetadataStore CreateLocalMetadataStore(
        string storePath,
        bool createIfNotExists,
        ILogger logger)
    {
        if (createIfNotExists && !Directory.Exists(storePath))
        {
            Directory.CreateDirectory(storePath);
            logger.LogInformation("Created metadata directory: {Path}", storePath);
        }

        if (!Directory.Exists(storePath))
        {
            throw new DirectoryNotFoundException($"Metadata store directory not found: {storePath}");
        }

        logger.LogInformation("Using local file system for metadata store: '{Path}'", storePath);
        return Microsoft.PackageGraph.Storage.Local.PackageStore.OpenOrCreate(storePath);
    }

    private static IContentStore CreateAzureBlobContentStore(
        string? connectionString,
        string containerName,
        bool createIfNotExists,
    ILogger logger)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string required for Azure Blob content stores");
        }

        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (createIfNotExists)
            {
                var createResponse = containerClient.CreateIfNotExists();
                if (createResponse != null)
                {
                    logger.LogInformation("Created Azure Blob container: {ContainerName}", containerName);
                }
                else
                {
                    logger.LogInformation("Azure Blob container already exists: {ContainerName}", containerName);
                }
            }

            logger.LogInformation(
                      "Successfully connected to Azure Storage account: {AccountName}",
                  blobServiceClient.AccountName);

            return BlobContentStore.OpenOrCreate(blobServiceClient, containerName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Azure Blob Storage for content store");
            throw;
        }
    }

    private static IContentStore CreateLocalContentStore(
        string storePath,
   bool createIfNotExists,
        ILogger logger)
    {
        if (createIfNotExists && !Directory.Exists(storePath))
        {
            Directory.CreateDirectory(storePath);
            logger.LogInformation("Created content directory: {Path}", storePath);
        }

        if (!Directory.Exists(storePath))
        {
            throw new DirectoryNotFoundException($"Content store directory not found: {storePath}");
        }

        logger.LogInformation("Using local file system for content store: '{Path}'", storePath);
        return new FileSystemContentStore(storePath);
    }
}

/// <summary>
/// Result of storage configuration validation.
/// </summary>
public class StorageValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}