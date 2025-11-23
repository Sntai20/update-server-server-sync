// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core;

using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using Microsoft.PackageGraph.Storage.Azure;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UpdateEngine.Core.HealthChecks;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;
using UpdateEngine.Services;

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
        services.Configure<AppConfig>(configuration.GetSection(AppConfig.SectionName));

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

        // 4. Orchestrators (host-agnostic, use IOptionsMonitor for hot-reload)
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // 5. Domain services
        services.AddSingleton<ISyncService, SyncService>();

        // 6. Stores (use IOptions - startup only since stores don't hot-reload)
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            var storageConfig = config.StorageConfiguration;

            if (storageConfig.UseAzureStorageForMetadata)
            {
                // Azure Blob Storage
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConfig.AzureStorageConnectionString);
                var container = blobServiceClient.GetBlobContainerClient(storageConfig.MetadataContainerName);
                return Microsoft.PackageGraph.Storage.Azure.PackageStore.Open(container);
            }
            else
            {
                // Local File System
                return Microsoft.PackageGraph.Storage.Local.PackageStore.Open(storageConfig.MetadataPath);
            }
        });

        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            var storageConfig = config.StorageConfiguration;

            if (string.IsNullOrEmpty(storageConfig.ContentPath) && !storageConfig.UseAzureStorageForContent)
            {
                return (IContentStore?)null;
            }

            if (storageConfig.UseAzureStorageForContent)
            {
                // Azure Blob Storage
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConfig.AzureStorageConnectionString);
                return (IContentStore?)Microsoft.PackageGraph.Storage.Azure.BlobContentStore.OpenOrCreate(
                    blobServiceClient, 
                    storageConfig.ContentContainerName, 
                    pathPrefix: "content");
            }
            else
            {
                // Local File System
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

        // 8. HTTP Client
        services.AddHttpClient();

        return services;
    }
}
