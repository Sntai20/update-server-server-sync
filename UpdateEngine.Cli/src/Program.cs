// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Cli;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using UpdateEngine.Cli.Commands;
using UpdateEngine.Cli.Configuration;
using UpdateEngine.Cli.Services;
using UpdateEngine.Core;

/// <summary>
/// UpdateEngine CLI tool for querying and managing updates.
/// Updated to work with unified function endpoints.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Build service provider
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Create command handlers
        var commandHandlers = serviceProvider.GetRequiredService<CommandHandlers>();

        // Create root command
        var rootCommand = new RootCommand("UpdateEngine CLI - Query and manage Microsoft Update Server-Server Sync (Unified API)")
        {
            CreateHealthCommand(commandHandlers),
            CreateConfigCommand(commandHandlers),
            CreateSyncCommand(commandHandlers),
            CreateStatsCommand(commandHandlers),
            CreateContentStatusCommand(commandHandlers),
            CreateSearchCommand(commandHandlers),
            CreateDetailsCommand(commandHandlers),
            CreateReindexCommand(commandHandlers),
            CreateCategoriesCommand(commandHandlers),
            CreateDownloadCommand(commandHandlers),
            CreateServer2022Command(commandHandlers),
            CreateServer2025Command(commandHandlers),
            CreateWindows11Command(commandHandlers)
        };

        // Add global options
        var urlOption = new Option<string>(
            aliases: new[] { "--url", "-u" },
            description: "UpdateEngine base URL",
            getDefaultValue: () => "http://localhost:7071");

        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout", "-t" },
            description: "Request timeout in seconds",
            getDefaultValue: () => 300);

        rootCommand.AddGlobalOption(urlOption);
        rootCommand.AddGlobalOption(timeoutOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // Determine CLI mode (local orchestrators vs remote HTTP)
        var cliMode = configuration.GetValue<string>("CliMode") ?? "local";
        
        if (cliMode.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            // Local mode: Register UpdateEngine.Core services (orchestrators, services, stores, health checks)
            services.AddUpdateEngineCore(configuration);
            
            // Register CommandHandlers with orchestrators
            services.AddTransient<CommandHandlers>(sp =>
            {
                var syncOrchestrator = sp.GetRequiredService<UpdateEngine.Core.Orchestrators.ISyncOrchestrator>();
                var metadataOrchestrator = sp.GetRequiredService<UpdateEngine.Core.Orchestrators.IMetadataOrchestrator>();
                var contentOrchestrator = sp.GetRequiredService<UpdateEngine.Core.Orchestrators.IContentOrchestrator>();
                var healthService = sp.GetRequiredService<UpdateEngine.Core.Services.IHealthService>();
                var metadataStore = sp.GetRequiredService<UpdateEngine.Metadata.Storage.IMetadataStore>();
                
                return new CommandHandlers(syncOrchestrator, metadataOrchestrator, contentOrchestrator, healthService, metadataStore);
            });
        }
        else
        {
            // Remote mode: Use HTTP client
            services.AddHttpClient<UpdateEngineClient>();
            
            // Register CommandHandlers with HTTP client
            services.AddTransient<CommandHandlers>(sp =>
            {
                var client = sp.GetRequiredService<UpdateEngineClient>();
                return new CommandHandlers(client);
            });
        }
    }

    private static Command CreateHealthCommand(CommandHandlers handlers)
    {
        var command = new Command("health", "Check UpdateEngine health status");
        
        var scopeOption = new Option<string>(
            aliases: new[] { "--scope", "-s" },
            description: "Health check scope: basic, full, sync, or store",
            getDefaultValue: () => "basic");
        
        command.AddOption(scopeOption);
        command.SetHandler(handlers.HandleHealthAsync, scopeOption);
        return command;
    }

    private static Command CreateConfigCommand(CommandHandlers handlers)
    {
        var command = new Command("config", "Get UpdateEngine configuration");
        command.SetHandler(handlers.HandleConfigurationAsync);
        return command;
    }

    private static Command CreateSyncCommand(CommandHandlers handlers)
    {
        var command = new Command("sync", "Trigger synchronization operations");

        var metadataCommand = new Command("metadata", "Sync metadata from upstream (comprehensive)");
        metadataCommand.SetHandler(handlers.HandleSyncMetadataAsync);

        var contentCommand = new Command("content", "Sync content from upstream");
        var daysBackOption = new Option<int>(
            aliases: new[] { "--days-back", "-d" },
            description: "Number of days back to sync content",
            getDefaultValue: () => 30);
        contentCommand.AddOption(daysBackOption);
        contentCommand.SetHandler(handlers.HandleSyncContentAsync, daysBackOption);

        var criticalCommand = new Command("critical", "Sync only critical and security updates");
        criticalCommand.SetHandler(handlers.HandleSyncCriticalAsync);

        command.AddCommand(metadataCommand);
        command.AddCommand(contentCommand);
        command.AddCommand(criticalCommand);

        return command;
    }

    private static Command CreateStatsCommand(CommandHandlers handlers)
    {
        var command = new Command("stats", "Get store statistics");
        command.SetHandler(handlers.HandleStoreStatisticsAsync);
        return command;
    }

    private static Command CreateContentStatusCommand(CommandHandlers handlers)
    {
        var command = new Command("content-status", "Get content synchronization status");
        command.SetHandler(handlers.HandleContentStatusAsync);
        return command;
    }

    private static Command CreateSearchCommand(CommandHandlers handlers)
    {
        var command = new Command("search", "Search for updates");

        var categoryArgument = new Argument<string>(
            name: "category",
            description: "Category to search in (e.g., 'Security Updates', 'Critical Updates')");

        command.AddArgument(categoryArgument);
        command.SetHandler(handlers.HandleSearchAsync, categoryArgument);

        return command;
    }

    private static Command CreateDetailsCommand(CommandHandlers handlers)
    {
        var command = new Command("details", "Get update details");

        var updateIdArgument = new Argument<string>(
            name: "update-id",
            description: "Update ID to get details for");

        command.AddArgument(updateIdArgument);
        command.SetHandler(handlers.HandleUpdateDetailsAsync, updateIdArgument);

        return command;
    }

    private static Command CreateReindexCommand(CommandHandlers handlers)
    {
        var command = new Command("reindex", "Check and reindex the metadata store if needed");
        command.SetHandler(handlers.HandleReindexAsync);
        return command;
    }

    private static Command CreateCategoriesCommand(CommandHandlers handlers)
    {
        var command = new Command("categories", "Get available update categories");
        command.SetHandler(handlers.HandleCategoriesAsync);
        return command;
    }

    private static Command CreateDownloadCommand(CommandHandlers handlers)
    {
        var downloadCommand = new Command("download", "Download update metadata or content files");

        // download list subcommand
        var listCommand = new Command("list", "List available downloads for an update");
        var listUpdateIdArgument = new Argument<string>("updateId", "The update ID");
        listCommand.AddArgument(listUpdateIdArgument);
        listCommand.SetHandler(handlers.HandleDownloadListAsync, listUpdateIdArgument);

        // download metadata subcommand
        var metadataCommand = new Command("metadata", "Download update metadata as JSON");
        var metadataUpdateIdArgument = new Argument<string>("updateId", "The update ID");
        var metadataOutputOption = new Option<string?>("--output", "Output file path (default: {updateId}_metadata.json)");
        metadataCommand.AddArgument(metadataUpdateIdArgument);
        metadataCommand.AddOption(metadataOutputOption);
        metadataCommand.SetHandler(handlers.HandleDownloadMetadataAsync, metadataUpdateIdArgument, metadataOutputOption);

        // download content subcommand
        var contentCommand = new Command("content", "Download update content files");
        var contentUpdateIdArgument = new Argument<string>("updateId", "The update ID");
        var contentOutputOption = new Option<string?>("--output", "Output file path (default: {updateId}_content)");
        var progressOption = new Option<bool>("--progress", "Show download progress");
        contentCommand.AddArgument(contentUpdateIdArgument);
        contentCommand.AddOption(contentOutputOption);
        contentCommand.AddOption(progressOption);
        contentCommand.SetHandler(handlers.HandleDownloadContentAsync, contentUpdateIdArgument, contentOutputOption, progressOption);

        // Add subcommands to download command
        downloadCommand.Add(listCommand);
        downloadCommand.Add(metadataCommand);
        downloadCommand.Add(contentCommand);

        return downloadCommand;
    }

    private static Command CreateServer2022Command(CommandHandlers handlers)
    {
        var server2022Command = new Command("server2022", "Download Windows Server 2022 updates");

        var downloadPathArgument = new Argument<string>("downloadPath", "Directory to download updates to");
        var securityOnlyOption = new Option<bool>("--security-only", "Download only security updates (default: false)");
        var maxUpdatesOption = new Option<int>("--max-updates", () => 50, "Maximum number of updates to process");
        var skipSyncOption = new Option<bool>("--skip-sync", "Skip metadata synchronization");
        var ipakCompatibleOption = new Option<bool>("--ipak-compatible", "Store updates in IPAK-compatible folder structure");

        server2022Command.AddArgument(downloadPathArgument);
        server2022Command.AddOption(securityOnlyOption);
        server2022Command.AddOption(maxUpdatesOption);
        server2022Command.AddOption(skipSyncOption);
        server2022Command.AddOption(ipakCompatibleOption);

        server2022Command.SetHandler(
            handlers.HandleDownloadServer2022Async,
            downloadPathArgument,
            securityOnlyOption,
            maxUpdatesOption,
            skipSyncOption,
            ipakCompatibleOption
        );

        return server2022Command;
    }

    private static Command CreateServer2025Command(CommandHandlers handlers)
    {
        var server2025Command = new Command("server2025", "Download Windows Server 2025 updates");

        var downloadPathArgument = new Argument<string>("downloadPath", "Directory to download updates to");
        var securityOnlyOption = new Option<bool>("--security-only", "Download only security updates (default: false)");
        var maxUpdatesOption = new Option<int>("--max-updates", () => 50, "Maximum number of updates to process");
        var skipSyncOption = new Option<bool>("--skip-sync", "Skip metadata synchronization");
        var ipakCompatibleOption = new Option<bool>("--ipak-compatible", "Store updates in IPAK-compatible folder structure");

        server2025Command.AddArgument(downloadPathArgument);
        server2025Command.AddOption(securityOnlyOption);
        server2025Command.AddOption(maxUpdatesOption);
        server2025Command.AddOption(skipSyncOption);
        server2025Command.AddOption(ipakCompatibleOption);

        server2025Command.SetHandler(
            handlers.HandleDownloadServer2025Async,
            downloadPathArgument,
            securityOnlyOption,
            maxUpdatesOption,
            skipSyncOption,
            ipakCompatibleOption
        );

        return server2025Command;
    }

    private static Command CreateWindows11Command(CommandHandlers handlers)
    {
        var windows11Command = new Command("windows11", "Download Windows 11 updates");

        var downloadPathArgument = new Argument<string>("downloadPath", "Directory to download updates to");
        var securityOnlyOption = new Option<bool>("--security-only", "Download only security updates (default: false)");
        var maxUpdatesOption = new Option<int>("--max-updates", () => 50, "Maximum number of updates to process");
        var skipSyncOption = new Option<bool>("--skip-sync", "Skip metadata synchronization");
        var ipakCompatibleOption = new Option<bool>("--ipak-compatible", "Store updates in IPAK-compatible folder structure");

        windows11Command.AddArgument(downloadPathArgument);
        windows11Command.AddOption(securityOnlyOption);
        windows11Command.AddOption(maxUpdatesOption);
        windows11Command.AddOption(skipSyncOption);
        windows11Command.AddOption(ipakCompatibleOption);

        windows11Command.SetHandler(
            handlers.HandleDownloadWindows11Async,
            downloadPathArgument,
            securityOnlyOption,
            maxUpdatesOption,
            skipSyncOption,
            ipakCompatibleOption
        );

        return windows11Command;
    }
}