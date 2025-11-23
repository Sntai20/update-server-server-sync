// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

/// <summary>
/// Storage configuration settings (Azure Blob Storage or local file system).
/// These settings are typically configured at startup and do not hot-reload.
/// </summary>
public class StorageConfiguration
{
    // Local storage paths
    public string MetadataPath { get; set; } = "./LocalMetadataStore";
    public string ContentPath { get; set; } = "./LocalContentStore";

    // Azure Storage settings
    public bool UseAzureStorageForMetadata { get; set; }
    public bool UseAzureStorageForContent { get; set; }
    public string AzureStorageConnectionString { get; set; } = string.Empty;
    public string AzureStorageAccountName { get; set; } = string.Empty;
    
    // Container names
    public string MetadataContainerName { get; set; } = "metadata";
    public string ContentContainerName { get; set; } = "content";
    public string ReportsContainerName { get; set; } = "reports";
    
    // Content path prefix
    public string ContentPathPrefix { get; set; } = "Content";

    // Startup behavior
    public bool ReindexOnStartup { get; set; }

    public void Validate()
    {
        if (this.UseAzureStorageForMetadata || this.UseAzureStorageForContent)
        {
            if (string.IsNullOrWhiteSpace(this.AzureStorageConnectionString) && 
                string.IsNullOrWhiteSpace(this.AzureStorageAccountName))
            {
                throw new InvalidOperationException(
                    "StorageConfiguration.AzureStorageConnectionString or AzureStorageAccountName is required when using Azure Storage");
            }
        }

        if (!this.UseAzureStorageForMetadata && string.IsNullOrWhiteSpace(this.MetadataPath))
        {
            throw new InvalidOperationException("StorageConfiguration.MetadataPath is required when not using Azure Storage");
        }
    }
}
