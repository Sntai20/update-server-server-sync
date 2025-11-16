// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;

namespace AppHost;

/// <summary>
/// Simplified configuration helper for mapping AppConfig to Azure Functions environment variables.
/// </summary>
public static class SimpleConfigurationHelper
{
    /// <summary>
    /// Maps configuration sections directly to environment variables without complex transformations.
    /// </summary>
    /// <param name="functions">The Azure Functions project resource builder to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void ConfigureUpdateFunctions(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        IConfiguration configuration)
    {
        // Bind simplified configuration
        var appConfig = new Configuration.AppConfig();
        configuration.Bind(appConfig);

        // Set storage environment variables
        functions
            .WithEnvironment("UseAzureStorageForMetadata", appConfig.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UseAzureStorageForContent", appConfig.UseAzureStorageForContent.ToString())
            .WithEnvironment("MetadataContainerName", appConfig.MetadataContainerName)
            .WithEnvironment("ContentContainerName", appConfig.ContentContainerName)
            .WithEnvironment("MetadataPath", appConfig.MetadataPath)
            .WithEnvironment("ContentPath", appConfig.ContentPath);

        // Set service configuration as JSON (for backward compatibility)
        var serviceConfig = new
        {
            ServiceUrl = appConfig.ServiceUrl,
            ContentUrl = appConfig.ContentUrl,
            MaxUpdateCount = appConfig.MaxUpdateCount
        };

        functions.WithEnvironment("ServiceConfigurationJson", JsonSerializer.Serialize(serviceConfig));

        // Set function schedules for timer triggers (with defaults)
        functions
            .WithEnvironment("SyncCriticalSchedule", appConfig.SyncCriticalSchedule)
            .WithEnvironment("SyncComprehensiveSchedule", appConfig.SyncComprehensiveSchedule)
            .WithEnvironment("SyncContentSchedule", appConfig.SyncContentSchedule)
            .WithEnvironment("ScheduledHealthCheckSchedule", appConfig.HealthCheckSchedule)
            .WithEnvironment("WeeklyMaintenanceSchedule", appConfig.WeeklyMaintenanceSchedule)
            .WithEnvironment("AnomalyDetectionSchedule", appConfig.AnomalyDetectionSchedule);

        // Set connection strings if provided
        SetConnectionStringIfExists(functions, configuration, "MetadataStorageConnection");
        SetConnectionStringIfExists(functions, configuration, "ContentStorageConnection");

        // Pass Azure Functions disable configuration
        var azureWebJobsSection = configuration.GetSection("AzureWebJobs");
        foreach (var job in azureWebJobsSection.GetChildren())
        {
            var disabledValue = job.GetValue<bool>("Disabled");
            functions.WithEnvironment($"AzureWebJobs.{job.Key}.Disabled", disabledValue.ToString().ToLower());
        }
    }

    /// <summary>
    /// Sets a connection string environment variable if it exists in configuration.
    /// </summary>
    private static void SetConnectionStringIfExists(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        IConfiguration configuration,
        string connectionStringName)
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        if (!string.IsNullOrEmpty(connectionString))
        {
            functions.WithEnvironment($"ConnectionStrings__{connectionStringName}", connectionString);
        }
    }
}