// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;

/// <summary>
/// Extension methods for registering and configuring AppConfig in dependency injection.
/// Provides centralized configuration management for all environments.
/// </summary>
public static class ConfigurationServiceExtensions
{
    /// <summary>
    /// Adds AppConfig to the service collection, binding from IConfiguration.
    /// This is the main method for configuring the application from appsettings.json files.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration source (usually from appsettings.json)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the configuration directly to AppConfig
        var appConfig = new AppConfig();
        configuration.Bind(appConfig);

        // Validate the configuration
        appConfig.Validate();

        // Register as singleton
        services.AddSingleton(appConfig);

        return services;
    }

    /// <summary>
    /// Adds AppConfig with shared configuration loading from Configuration project.
    /// Loads shared base settings, then applies environment-specific overrides.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="environment">The current environment (Development, Production, etc.)</param>
    /// <param name="additionalConfiguration">Optional additional configuration to apply</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSharedAppConfiguration(this IServiceCollection services, string environment, IConfiguration? additionalConfiguration = null)
    {
        var configBuilder = new ConfigurationBuilder();

        // Get the path to the Configuration project's shared settings
        var configurationAssembly = Assembly.GetAssembly(typeof(AppConfig));
        var configDirectory = Path.GetDirectoryName(configurationAssembly?.Location) ?? throw new InvalidOperationException("Cannot locate Configuration assembly");
        var sharedPath = Path.Combine(configDirectory, "shared");

        // Load defaults first (base configuration)
        var defaultsPath = Path.Combine(sharedPath, "appsettings.defaults.json");
        if (File.Exists(defaultsPath))
        {
            configBuilder.AddJsonFile(defaultsPath, optional: false, reloadOnChange: false);
        }

        // Load environment-specific shared configuration
        var sharedEnvPath = Path.Combine(sharedPath, $"appsettings.{environment}.json");
        if (File.Exists(sharedEnvPath))
        {
            configBuilder.AddJsonFile(sharedEnvPath, optional: true, reloadOnChange: false);
        }

        // Add any additional configuration (project-specific overrides)
        if (additionalConfiguration != null)
        {
            configBuilder.AddConfiguration(additionalConfiguration);
        }

        var configuration = configBuilder.Build();

        // Bind to AppConfig and register
        var appConfig = new AppConfig();
        configuration.Bind(appConfig);
        appConfig.Validate();

        services.AddSingleton(appConfig);

        return services;
    }

    /// <summary>
    /// Adds AppConfig to the service collection with a pre-configured instance.
    /// Useful for testing or when configuration comes from sources other than appsettings.json.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="appConfig">The pre-configured AppConfig instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, AppConfig appConfig)
    {
        appConfig.Validate();
        services.AddSingleton(appConfig);
        return services;
    }

    /// <summary>
    /// Adds AppConfig to the service collection using a configuration delegate.
    /// Useful for programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">The configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, Action<AppConfig> configureOptions)
    {
        var appConfig = new AppConfig();
        configureOptions(appConfig);

        appConfig.Validate();
        services.AddSingleton(appConfig);

        return services;
    }
}

/// <summary>
/// Extension methods for working with AppConfig instances.
/// </summary>
public static class AppConfigExtensions
{
    /// <summary>
    /// Converts a schedule string to a TimeSpan.
    /// Supports formats like "02:00:00" and "1.02:00:00".
    /// </summary>
    /// <param name="scheduleString">The schedule string</param>
    /// <returns>The parsed TimeSpan</returns>
    public static TimeSpan ParseSchedule(this string scheduleString)
    {
        if (string.IsNullOrWhiteSpace(scheduleString))
            throw new ArgumentException("Schedule string cannot be null or empty", nameof(scheduleString));

        if (TimeSpan.TryParse(scheduleString, out var timeSpan))
            return timeSpan;

        throw new ArgumentException($"Invalid schedule format: {scheduleString}", nameof(scheduleString));
    }

    /// <summary>
    /// Gets the effective metadata storage type based on configuration.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>The storage type description</returns>
    public static string GetMetadataStorageType(this AppConfig config)
    {
        return config.UseAzureStorageForMetadata ? "Azure Blob Storage" : "Local File System";
    }

    /// <summary>
    /// Gets the effective content storage type based on configuration.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>The storage type description</returns>
    public static string GetContentStorageType(this AppConfig config)
    {
        return config.UseAzureStorageForContent ? "Azure Blob Storage" : "Local File System";
    }

    /// <summary>
    /// Creates a summary of the current configuration for logging/debugging.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>A formatted configuration summary</returns>
    public static string GetConfigurationSummary(this AppConfig config)
    {
        return $@"Microsoft Update Server Configuration:
  Service URL: {config.ServiceUrl}
  Content URL: {config.ContentUrl}
  Max Updates: {config.MaxUpdateCount}
  Categories: {string.Join(", ", config.SupportedCategories)}
  Languages: {string.Join(", ", config.SupportedLanguages)}
  
  Storage:
    Metadata: {config.GetMetadataStorageType()} ({config.MetadataPath})
    Content: {config.GetContentStorageType()} ({config.ContentPath})
    Metadata Container: {config.MetadataContainerName}
    Content Container: {config.ContentContainerName}
    Reindex on Startup: {config.ReindexOnStartup}
  
  Schedules:
    Critical Sync: {config.SyncCriticalSchedule}
    Comprehensive Sync: {config.SyncComprehensiveSchedule}
    Content Sync: {config.SyncContentSchedule}
    Health Check: {config.ScheduledHealthCheckSchedule}
    Maintenance: {config.MaintenanceSchedule}
    Weekly Maintenance: {config.WeeklyMaintenanceSchedule}
    Anomaly Detection: {config.AnomalyDetectionSchedule}
  
  Features:
    Scheduled Sync: {config.EnableScheduledSync}
    Detailed Logging: {config.EnableDetailedLogging}
    Metrics: {config.EnableMetrics}
    Caching: {config.EnableCaching}";
    }
}

/// <summary>
/// Extension methods for IConfigurationBuilder to add shared configuration files.
/// </summary>
public static class SharedConfigurationExtensions
{
    /// <summary>
    /// Adds shared configuration files from the Configuration project to the configuration builder.
    /// Loads: defaults -> shared overrides -> environment-specific overrides.
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddSharedAppConfiguration(this IConfigurationBuilder builder)
    {
        // Get the Configuration assembly location
        var configAssembly = typeof(ConfigurationServiceExtensions).Assembly;
        var baseDirectory = Path.GetDirectoryName(configAssembly.Location)
            ?? throw new InvalidOperationException("Could not determine assembly directory");

        var sharedPath = Path.Combine(baseDirectory, "shared");

        // Load configuration files in order: defaults -> shared overrides -> environment overrides  
        var defaultsPath = Path.Combine(sharedPath, "appsettings.defaults.json");
        if (File.Exists(defaultsPath))
        {
            builder.AddJsonFile(defaultsPath, optional: false, reloadOnChange: true);
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var environmentFile = Path.Combine(sharedPath, $"appsettings.{environment}.json");
        if (File.Exists(environmentFile))
        {
            builder.AddJsonFile(environmentFile, optional: true, reloadOnChange: true);
        }

        return builder;
    }
}