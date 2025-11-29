// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core;

using UpdateEngine.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpdateEngine.Metadata.Storage;
using UpdateEngine.Metadata.Storage.Local;
using UpdateEngine.Metadata.Storage.Azure;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UpdateEngine.Core.HealthChecks;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;

/// <summary>
/// Extension methods for registering Update Engine services in the DI container.
/// This provides centralized configuration for all hosting models (Azure Functions, Worker Service, CLI).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Update Engine core services to the service collection.
    /// This includes configuration, orchestrators, domain services, stores, caching, and health checks.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration source</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUpdateEngineCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. JSON Serialization Options (required for caching and API responses)
        services.AddSingleton<JsonSerializerOptions>(provider => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        });

        // 2. Configuration (IOptionsMonitor pattern with hot-reload)
        services.Configure<AppConfig>(options => configuration.GetSection(AppConfig.SectionName).Bind(options));

        // Validate configuration on startup
        var appConfig = configuration.BindToAppConfig();

        // 3. Distributed Caching (Redis)
        if (appConfig.CacheConfiguration.EnableDistributedCache)
        {
            // Try to get Redis connection from Aspire first (ConnectionStrings:Redis)
            // Fall back to CacheConfiguration.RedisConnectionString if not available
            var redisConnection = configuration.GetConnectionString("Redis") 
                ?? appConfig.CacheConfiguration.RedisConnectionString;

            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = $"{appConfig.CacheConfiguration.KeyPrefix}:";
                });
            }
        }

        // Register CacheService
        services.AddSingleton<CacheService>();

        // 4. Domain Services (required by orchestrators)
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<IQueryService, QueryService>();
        services.AddSingleton<IHealthService, HealthService>();
        services.AddSingleton<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddSingleton<IQueueService, QueueService>();

        // 4a. Maintenance Services
        services.AddSingleton<UpdateEngine.Core.Maintenance.MetadataStoreCleanup>();

        // 4b. Downstream Sync Service (for WorkerService pulling from Functions)
        // Configure HttpClient with Aspire service discovery
        // The service name "http://UpdateEngine" is automatically resolved by Aspire
        var downstreamConfig = appConfig.DownstreamConfiguration;
        if (downstreamConfig.SyncFromUpstream && !string.IsNullOrEmpty(downstreamConfig.UpstreamFunctionsUrl))
        {
            services.AddHttpClient<IDownstreamSyncService, DownstreamSyncService>(client =>
            {
                // Aspire service discovery: "http://UpdateEngine" resolves to the actual dynamic port
                client.BaseAddress = new Uri(downstreamConfig.UpstreamFunctionsUrl);
                client.Timeout = downstreamConfig.HttpTimeout;
            });
            // Note: AddStandardResilienceHandler() requires Microsoft.Extensions.Http.Resilience package
            // For now, basic HttpClient with timeout is sufficient
        }
        else
        {
            // Register a no-op implementation when downstream sync is disabled
            services.AddSingleton<IDownstreamSyncService>(provider => 
                new NoOpDownstreamSyncService());
        }

        // 5. Orchestrators (host-agnostic, use IOptionsMonitor for hot-reload)
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // 6. Stores (use IOptions - startup only since stores don't hot-reload)
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<IMetadataStore>>();
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            var storageConfig = config.StorageConfiguration;

            // Try multiple connection string sources in order of precedence:
            // 1. Direct environment variable (Aspire flat format): MetadataStorageConnection
            // 2. ConnectionStrings section: ConnectionStrings__MetadataStorageConnection  
            // 3. Configuration value: StorageConfiguration.AzureStorageConnectionString
            var connectionString = Environment.GetEnvironmentVariable("MetadataStorageConnection")
                ?? configuration.GetConnectionString("MetadataStorageConnection") 
                ?? storageConfig.AzureStorageConnectionString;

            // Log configuration for debugging
            logger?.LogInformation("Metadata Store Configuration:");
            logger?.LogInformation("  UseAzureStorageForMetadata: {UseAzure}", storageConfig.UseAzureStorageForMetadata);
            logger?.LogInformation("  MetadataPath: {MetadataPath}", storageConfig.MetadataPath);
            logger?.LogInformation("  MetadataContainerName: {ContainerName}", storageConfig.MetadataContainerName);
            logger?.LogInformation("  Connection String from Environment: {HasEnvVar}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MetadataStorageConnection")));
            logger?.LogInformation("  Connection String from ConnectionStrings: {HasConnectionString}", !string.IsNullOrEmpty(configuration.GetConnectionString("MetadataStorageConnection")));
            logger?.LogInformation("  Connection String from Config: {HasConfig}", !string.IsNullOrEmpty(storageConfig.AzureStorageConnectionString));
            logger?.LogInformation("  Final Connection String Available: {HasConnection}", !string.IsNullOrEmpty(connectionString));

            // Decide whether to use Azure Storage based on:
            // 1. Configuration flag is true AND connection string is available, OR
            // 2. Connection string is available (Aspire-provided)
            var useAzureStorage = (storageConfig.UseAzureStorageForMetadata && !string.IsNullOrEmpty(connectionString))
                || (!string.IsNullOrEmpty(connectionString));

            if (useAzureStorage)
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    logger?.LogWarning(
                        "Azure Storage requested but no connection string found. " +
                        "Falling back to local file system storage at: {Path}", 
                        storageConfig.MetadataPath);
                    
                    // Fall back to local storage - use OpenOrCreate to create if needed
                    return UpdateEngine.Metadata.Storage.Local.PackageStore.OpenOrCreate(storageConfig.MetadataPath);
                }

                logger?.LogInformation("Opening Azure Blob Storage metadata store (container: {Container})", storageConfig.MetadataContainerName);
                // Azure Blob Storage - use OpenOrCreate to create container if it doesn't exist
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
                return UpdateEngine.Metadata.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, storageConfig.MetadataContainerName);
            }
            else
            {
                logger?.LogInformation("Opening local file system metadata store at: {Path}", storageConfig.MetadataPath);
                // Local File System - use OpenOrCreate to create if needed
                return UpdateEngine.Metadata.Storage.Local.PackageStore.OpenOrCreate(storageConfig.MetadataPath);
            }
        });

        services.AddSingleton(provider =>
        {
            var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<IContentStore>>();
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            var storageConfig = config.StorageConfiguration;

            if (string.IsNullOrEmpty(storageConfig.ContentPath) && !storageConfig.UseAzureStorageForContent)
            {
                return (IContentStore?)null;
            }

            // Try multiple connection string sources in order of precedence:
            // 1. Direct environment variable (Aspire flat format): ContentStorageConnection
            // 2. ConnectionStrings section: ConnectionStrings__ContentStorageConnection
            // 3. Fall back to MetadataStorageConnection if ContentStorageConnection not found
            // 4. Configuration value: StorageConfiguration.AzureStorageConnectionString
            var connectionString = Environment.GetEnvironmentVariable("ContentStorageConnection")
                ?? configuration.GetConnectionString("ContentStorageConnection") 
                ?? Environment.GetEnvironmentVariable("MetadataStorageConnection")
                ?? configuration.GetConnectionString("MetadataStorageConnection")
                ?? storageConfig.AzureStorageConnectionString;

            // Decide whether to use Azure Storage based on:
            // 1. Configuration flag is true AND connection string is available, OR
            // 2. Connection string is available (Aspire-provided)
            var useAzureStorage = (storageConfig.UseAzureStorageForContent && !string.IsNullOrEmpty(connectionString))
                || (!string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(storageConfig.ContentPath));

            if (useAzureStorage)
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    logger?.LogWarning(
                        "Azure Storage requested but no connection string found. " +
                        "Falling back to local file system storage at: {Path}", 
                        storageConfig.ContentPath);
                    
                    // Fall back to local storage
                    return (IContentStore?)new FileSystemContentStore(storageConfig.ContentPath);
                }

                logger?.LogInformation("Opening Azure Blob Storage content store (container: {Container})", storageConfig.ContentContainerName);
                // Azure Blob Storage
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
                return (IContentStore?)UpdateEngine.Metadata.Storage.Azure.BlobContentStore.OpenOrCreate(
                    blobServiceClient, 
                    storageConfig.ContentContainerName, 
                    pathPrefix: "content");
            }
            else
            {
                // Local File System
                if (string.IsNullOrEmpty(storageConfig.ContentPath))
                {
                    logger?.LogInformation("No content store configured (catalog-only mode)");
                    return (IContentStore?)null;
                }

                logger?.LogInformation("Opening local file system content store at: {Path}", storageConfig.ContentPath);
                return (IContentStore?)new FileSystemContentStore(storageConfig.ContentPath);
            }
        });

        // 7. Health Checks
        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck<MetadataStoreHealthCheck>(
                name: "metadata-store",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: new[] { "storage", "critical" })
            .AddCheck<ContentStoreHealthCheck>(
                name: "content-store",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "storage" })
            .AddCheck<UpstreamConnectionHealthCheck>(
                name: "upstream-connection",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "network" })
            .AddCheck<AzureBlobStorageHealthCheck>(
                name: "azure-storage",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: new[] { "storage", "azure", "critical" });

        // Add Redis health check if caching is enabled
        if (appConfig.CacheConfiguration.EnableDistributedCache)
        {
            healthChecksBuilder.AddCheck<RedisHealthCheck>(
                name: "redis-cache",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "cache", "redis" });
        }

        return services;
    }
}
