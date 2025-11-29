// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Cli.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using UpdateEngine.Core.Maintenance;

/// <summary>
/// CLI command to clean up corrupted metadata stores.
/// </summary>
public class CleanupCommand : Command
{
    public CleanupCommand() : base(
        "cleanup",
        "Clean up corrupted metadata stores and content directories")
    {
        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run", "-n" },
            description: "Preview what would be deleted without actually deleting",
            getDefaultValue: () => false);

        var includeAzuriteOption = new Option<bool>(
            aliases: new[] { "--include-azurite", "-a" },
            description: "Also clean Azurite storage emulator data",
            getDefaultValue: () => false);

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Skip confirmation prompts",
            getDefaultValue: () => false);

        var baseDirOption = new Option<string?>(
            aliases: new[] { "--base-dir", "-b" },
            description: "Base directory to search from (defaults to current directory)");

        this.AddOption(dryRunOption);
        this.AddOption(includeAzuriteOption);
        this.AddOption(forceOption);
        this.AddOption(baseDirOption);

        this.SetHandler(async (context) =>
        {
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var includeAzurite = context.ParseResult.GetValueForOption(includeAzuriteOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var baseDir = context.ParseResult.GetValueForOption(baseDirOption);

            await this.ExecuteAsync(dryRun, includeAzurite, force, baseDir, context.GetCancellationToken());
        });
    }

    private async Task ExecuteAsync(
        bool dryRun,
        bool includeAzurite,
        bool force,
        string? baseDir,
        CancellationToken cancellationToken)
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<MetadataStoreCleanup>();
        var cleanup = new MetadataStoreCleanup(logger);

        // Display header
        Console.WriteLine();
        Console.WriteLine("??????????????????????????????????????????????????????????????????");
        Console.WriteLine("?  Clean Corrupted Metadata Stores                               ?");
        Console.WriteLine("??????????????????????????????????????????????????????????????????");
        Console.WriteLine();

        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("? DRY RUN MODE - No changes will be made");
            Console.ResetColor();
            Console.WriteLine();
        }

        // Prepare options
        var options = new MetadataStoreCleanup.CleanupOptions
        {
            DryRun = dryRun,
            IncludeAzurite = includeAzurite,
            BaseDirectory = baseDir
        };

        // Execute cleanup
        var result = await cleanup.CleanupCorruptedStoresAsync(options, cancellationToken);

        // Display results
        Console.WriteLine();
        Console.WriteLine("???????????????????????????????????????????????????????????????");
        Console.WriteLine();

        if (result.DirectoriesFound == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("? No corrupted stores found. All clean!");
            Console.ResetColor();
            return;
        }

        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"? DRY RUN - Would delete {result.DirectoriesFound} directory(ies)");
            Console.WriteLine($"  Total size: {this.FormatBytes(result.TotalSizeBytes)}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Run without --dry-run to actually delete these directories.");
            return;
        }

        // Show results
        if (result.DirectoriesCleaned > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"? Cleaned: {result.DirectoriesCleaned} directory(ies)");
            Console.WriteLine($"  Freed disk space: {this.FormatBytes(result.FreedSizeBytes)}");
            Console.ResetColor();
        }

        if (result.DirectoriesFailed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"? Failed: {result.DirectoriesFailed} directory(ies)");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Common solutions:");
            Console.WriteLine("  1. Stop AppHost (Ctrl+C in the terminal)");
            Console.WriteLine("  2. Close Visual Studio and any IDEs");
            Console.WriteLine("  3. Run as Administrator");
            Console.ResetColor();
        }

        if (result.LockingProcesses.Any())
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"? Found {result.LockingProcesses.Count} process(es) that might lock stores:");
            foreach (var process in result.LockingProcesses)
            {
                Console.WriteLine($"  • {process}");
            }
            Console.ResetColor();
        }

        // Show next steps if successful
        if (result.Success && result.DirectoriesCleaned > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Next steps:");
            Console.ResetColor();
            Console.WriteLine("  1. Start AppHost: cd UpdateEngine.AppHost/src && dotnet run");
            Console.WriteLine("  2. Fresh stores will be created automatically");
            Console.WriteLine("  3. Verify startup in Aspire Dashboard");
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
