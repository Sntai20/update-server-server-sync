// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using System.Text.Json;

namespace AppHost;

/// <summary>
/// Simplified configuration helper for the Microsoft Update Server-Server Sync application.
/// Uses the IOptions pattern for clean, testable configuration management.
/// </summary>
public static class SimpleConfigurationHelper
{
    /// <summary>
    /// Configures environment variables for the Azure Functions project resource.
    /// Maps configuration sections directly to environment variables without complex transformations.
    /// </summary>
    /// <param name="functions">The Azure Functions project resource builder to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void ConfigureUpdateFunctions(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        IConfiguration configuration)
    {
        // Bind storage options
        var storage = new Configuration.StorageOptions();
        configuration.GetSection(Configuration.StorageOptions.SectionName).Bind(storage);

        // Bind server options  
        var server = new Configuration.UpdateServerOptions();
        configuration.GetSection(Configuration.UpdateServerOptions.SectionName).Bind(server);

        // Bind function schedules
        var schedules = new Configuration.FunctionScheduleOptions();
        configuration.GetSection(Configuration.FunctionScheduleOptions.SectionName).Bind(schedules);

        // Set storage environment variables
        functions
            .WithEnvironment("UseAzureStorageForMetadata", storage.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UseAzureStorageForContent", storage.UseAzureStorageForContent.ToString())
            .WithEnvironment("MetadataContainerName", storage.MetadataContainerName)
            .WithEnvironment("ContentContainerName", storage.ContentContainerName)
            .WithEnvironment("MetadataStorePath", storage.MetadataPath)
            .WithEnvironment("ContentStorePath", storage.ContentPath);

        // Set service configuration as JSON (for backward compatibility)
        var serviceConfig = new
        {
            ServiceUrl = server.ServiceUrl,
            ContentUrl = server.ContentUrl,
            MaxUpdateCount = server.MaxUpdateCount,
            SupportedCategories = server.SupportedCategories,
            SupportedLanguages = server.SupportedLanguages
        };

        functions.WithEnvironment("ServiceConfigurationJson", JsonSerializer.Serialize(serviceConfig));

        // Set function schedules for timer triggers
        functions
            .WithEnvironment("SyncCriticalSchedule", schedules.SyncCritical)
            .WithEnvironment("SyncComprehensiveSchedule", schedules.SyncComprehensive)
            .WithEnvironment("SyncContentSchedule", schedules.SyncContent)
            .WithEnvironment("ScheduledHealthCheckSchedule", schedules.HealthCheck)
            .WithEnvironment("WeeklyMaintenanceSchedule", schedules.WeeklyMaintenance)
            .WithEnvironment("AnomalyDetectionSchedule", schedules.AnomalyDetection);

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