// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Maintenance;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Provides functionality to clean up corrupted metadata stores and content directories.
/// Handles the "An item with the same key has already been added" startup error.
/// </summary>
public class MetadataStoreCleanup
{
    private readonly ILogger<MetadataStoreCleanup> logger;

    public MetadataStoreCleanup(ILogger<MetadataStoreCleanup> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Result of a cleanup operation.
    /// </summary>
    public class CleanupResult
    {
        public bool Success { get; set; }
        public int DirectoriesFound { get; set; }
        public int DirectoriesCleaned { get; set; }
        public int DirectoriesFailed { get; set; }
        public long TotalSizeBytes { get; set; }
        public long FreedSizeBytes { get; set; }
        public List<string> CleanedPaths { get; set; } = new();
        public List<string> FailedPaths { get; set; } = new();
        public List<string> LockingProcesses { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Options for cleanup operation.
    /// </summary>
    public class CleanupOptions
    {
        /// <summary>
        /// Preview mode - don't actually delete anything.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Include Azurite storage emulator data.
        /// </summary>
        public bool IncludeAzurite { get; set; }

        /// <summary>
        /// Base directory to search from (defaults to current directory).
        /// </summary>
        public string? BaseDirectory { get; set; }

        /// <summary>
        /// Additional custom paths to clean.
        /// </summary>
        public List<string>? CustomPaths { get; set; }
    }

    /// <summary>
    /// Scans for and optionally cleans corrupted metadata stores.
    /// </summary>
    public async Task<CleanupResult> CleanupCorruptedStoresAsync(
        CleanupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CleanupOptions();
        var result = new CleanupResult { Success = true };

        try
        {
            var baseDir = options.BaseDirectory ?? Directory.GetCurrentDirectory();
            
            this.logger.LogInformation("Scanning for corrupted metadata stores in: {BaseDir}", baseDir);

            // Get all paths to check
            var pathsToCheck = this.GetPathsToCheck(baseDir, options);

            // Find existing directories
            var foundDirectories = new List<(string Path, long Size)>();

            foreach (var path in pathsToCheck)
            {
                if (Directory.Exists(path))
                {
                    var size = this.GetDirectorySize(path);
                    foundDirectories.Add((path, size));
                    result.TotalSizeBytes += size;
                    
                    this.logger.LogWarning(
                        "Found corrupted store: {Path} ({Size})", 
                        path, 
                        this.FormatBytes(size));
                }
            }

            result.DirectoriesFound = foundDirectories.Count;

            if (foundDirectories.Count == 0)
            {
                this.logger.LogInformation("No corrupted stores found. All clean!");
                return result;
            }

            // Check for locking processes
            var lockingProcesses = this.GetLockingProcesses();
            if (lockingProcesses.Any())
            {
                result.LockingProcesses = lockingProcesses;
                this.logger.LogWarning(
                    "Found {Count} process(es) that might lock the stores: {Processes}",
                    lockingProcesses.Count,
                    string.Join(", ", lockingProcesses));
            }

            // Preview mode - stop here
            if (options.DryRun)
            {
                this.logger.LogInformation(
                    "DRY RUN - Would delete {Count} directory(ies) ({Size})",
                    foundDirectories.Count,
                    this.FormatBytes(result.TotalSizeBytes));
                return result;
            }

            // Perform cleanup
            this.logger.LogInformation("Cleaning {Count} corrupted store(s)...", foundDirectories.Count);

            foreach (var (path, size) in foundDirectories)
            {
                try
                {
                    this.logger.LogInformation("Removing: {Path}...", path);
                    
                    await Task.Run(() => Directory.Delete(path, recursive: true), cancellationToken);
                    
                    result.DirectoriesCleaned++;
                    result.FreedSizeBytes += size;
                    result.CleanedPaths.Add(path);
                    
                    this.logger.LogInformation("? Successfully removed: {Path}", path);
                }
                catch (UnauthorizedAccessException ex)
                {
                    result.DirectoriesFailed++;
                    result.FailedPaths.Add(path);
                    result.Success = false;
                    
                    this.logger.LogError(
                        ex,
                        "? Access denied: {Path}. Try running as Administrator.",
                        path);
                }
                catch (IOException ex)
                {
                    result.DirectoriesFailed++;
                    result.FailedPaths.Add(path);
                    result.Success = false;
                    
                    this.logger.LogError(
                        ex,
                        "? Directory locked: {Path}. Stop AppHost and try again.",
                        path);
                }
                catch (Exception ex)
                {
                    result.DirectoriesFailed++;
                    result.FailedPaths.Add(path);
                    result.Success = false;
                    
                    this.logger.LogError(ex, "? Error removing: {Path}", path);
                }
            }

            this.logger.LogInformation(
                "Cleanup complete: {Cleaned}/{Total} directories cleaned, {Freed} freed",
                result.DirectoriesCleaned,
                result.DirectoriesFound,
                this.FormatBytes(result.FreedSizeBytes));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            this.logger.LogError(ex, "Error during cleanup operation");
        }

        return result;
    }

    /// <summary>
    /// Gets all paths to check for corrupted stores.
    /// </summary>
    private List<string> GetPathsToCheck(string baseDir, CleanupOptions options)
    {
        var paths = new List<string>();

        // Functions local stores (source directory)
        paths.Add(Path.Combine(baseDir, "UpdateEngine.Functions", "src", "LocalMetadataStore"));
        paths.Add(Path.Combine(baseDir, "UpdateEngine.Functions", "src", "LocalContentStore"));

        // Output directory stores - all configurations
        var configs = new[] { "Debug", "Release" };
        var platforms = new[] { "", "x64" };

        foreach (var platform in platforms)
        {
            foreach (var config in configs)
            {
                var outPath = string.IsNullOrEmpty(platform)
                    ? Path.Combine(baseDir, "out", "UpdateEngine", config, "net9.0")
                    : Path.Combine(baseDir, "out", "UpdateEngine", platform, config, "net9.0");

                paths.Add(Path.Combine(outPath, "LocalMetadataStore"));
                paths.Add(Path.Combine(outPath, "LocalContentStore"));
            }
        }

        // WorkerService data directories
        paths.Add(Path.Combine(baseDir, "UpdateEngine.WorkerService", "src", "data", "metadata"));
        paths.Add(Path.Combine(baseDir, "UpdateEngine.WorkerService", "src", "data", "content"));
        paths.Add(Path.Combine(baseDir, "UpdateEngine.WorkerService", "src", "data"));
        paths.Add(Path.Combine(baseDir, "data", "metadata"));
        paths.Add(Path.Combine(baseDir, "data", "content"));
        paths.Add(Path.Combine(baseDir, "data"));

        // Azurite if requested
        if (options.IncludeAzurite)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            paths.Add(Path.Combine(userProfile, ".aspire", "azurite"));
            paths.Add(Path.Combine(localAppData, "Microsoft", "Azurite"));
        }

        // Custom paths
        if (options.CustomPaths != null)
        {
            paths.AddRange(options.CustomPaths);
        }

        return paths;
    }

    /// <summary>
    /// Gets the total size of a directory in bytes.
    /// </summary>
    private long GetDirectorySize(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Formats bytes to human-readable size.
    /// </summary>
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

    /// <summary>
    /// Gets list of processes that might be locking the directories.
    /// </summary>
    private List<string> GetLockingProcesses()
    {
        var lockingProcesses = new List<string>();

        try
        {
            // Check for dotnet processes
            var dotnetProcesses = Process.GetProcessesByName("dotnet");
            lockingProcesses.AddRange(
                dotnetProcesses.Select(p => $"dotnet (PID: {p.Id})"));

            // Check for func processes (Azure Functions Core Tools)
            var funcProcesses = Process.GetProcessesByName("func");
            lockingProcesses.AddRange(
                funcProcesses.Select(p => $"func (PID: {p.Id})"));
        }
        catch
        {
            // Ignore errors getting processes
        }

        return lockingProcesses;
    }
}
