// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Cli.Storage;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// IPAK-compatible content storage that organizes update files according to IPAK's expected folder structure.
/// Files are stored in UpdateFiles/{last2chars}/ folders based on the last 2 characters of the base filename.
/// </summary>
public class IpakCompatibleContentStore
{
    private readonly string baseContentPath;

    public IpakCompatibleContentStore(string baseContentPath)
    {
        this.baseContentPath = baseContentPath;
        this.InitializeDirectoryStructure();
    }

    /// <summary>
    /// Stores update content in IPAK-compatible folder structure.
    /// </summary>
    public async Task<string> StoreUpdateAsync(string updateId, string title, byte[] contentBytes, string? originalFilename = null)
    {
        var filename = this.ExtractOrGenerateFilename(updateId, title, originalFilename);
        var targetPath = this.GetIpakCompatiblePath(filename);
        
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, contentBytes);
        
        // Store metadata for reference
        await this.StoreUpdateMetadataAsync(updateId, title, filename, targetPath);
        
        return targetPath;
    }

    /// <summary>
    /// Gets the IPAK-compatible storage path for a filename.
    /// </summary>
    public string GetIpakCompatiblePath(string filename)
    {
        var baseFilename = Path.GetFileNameWithoutExtension(filename);
        var folderSuffix = baseFilename.Length >= 2 
            ? baseFilename.Substring(baseFilename.Length - 2).ToLowerInvariant()
            : baseFilename.ToLowerInvariant().PadLeft(2, '0');
        
        return Path.Combine(this.baseContentPath, "UpdateFiles", folderSuffix, filename);
    }

    /// <summary>
    /// Copies an existing file to IPAK-compatible location.
    /// </summary>
    public async Task<string> CopyToIpakStructureAsync(string sourceFilePath, string updateId, string title)
    {
        var sourceFilename = Path.GetFileName(sourceFilePath);
        var targetPath = this.GetIpakCompatiblePath(sourceFilename);
        
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        
        if (File.Exists(sourceFilePath))
        {
            File.Copy(sourceFilePath, targetPath, overwrite: true);
            
            // Store metadata for reference
            await this.StoreUpdateMetadataAsync(updateId, title, sourceFilename, targetPath);
            
            return targetPath;
        }
        
        throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
    }

    /// <summary>
    /// Gets folder suffix based on IPAK rules (last 2 characters of base filename).
    /// </summary>
    public static string GetFolderSuffix(string filename)
    {
        var baseFilename = Path.GetFileNameWithoutExtension(filename);
        return baseFilename.Length >= 2 
            ? baseFilename.Substring(baseFilename.Length - 2).ToLowerInvariant()
            : baseFilename.ToLowerInvariant().PadLeft(2, '0');
    }

    private void InitializeDirectoryStructure()
    {
        // Create the base IPAK directory structure
        var updateFilesPath = Path.Combine(this.baseContentPath, "UpdateFiles");
        var customUpdatesPath = Path.Combine(this.baseContentPath, "CustomUpdates");
        var wuAgentPath = Path.Combine(this.baseContentPath, "WUAgent");
        var metadataPath = Path.Combine(this.baseContentPath, "_metadata");
        
        Directory.CreateDirectory(updateFilesPath);
        Directory.CreateDirectory(customUpdatesPath);
        Directory.CreateDirectory(wuAgentPath);
        Directory.CreateDirectory(metadataPath);
    }

    private string ExtractOrGenerateFilename(string updateId, string title, string? originalFilename)
    {
        // If we have an original filename, use it
        if (!string.IsNullOrEmpty(originalFilename))
        {
            return originalFilename;
        }

        // Try to extract KB number and create a filename
        var kbMatch = Regex.Match(title, @"KB(\d+)", RegexOptions.IgnoreCase);
        if (kbMatch.Success)
        {
            var kbNumber = kbMatch.Groups[1].Value;
            
            // Determine file extension based on content type or title
            var extension = this.DetermineFileExtension(title);
            
            // Create a descriptive filename
            var baseFilename = $"kb{kbNumber}";
            
            // Add architecture if detected
            if (title.Contains("x64", StringComparison.OrdinalIgnoreCase))
            {
                baseFilename += "-x64";
            }
            else if (title.Contains("x86", StringComparison.OrdinalIgnoreCase) || title.Contains("32-bit", StringComparison.OrdinalIgnoreCase))
            {
                baseFilename += "-x86";
            }
            else if (title.Contains("arm64", StringComparison.OrdinalIgnoreCase))
            {
                baseFilename += "-arm64";
            }
            
            return $"{baseFilename}.{extension}";
        }

        // Fallback: use update ID
        return $"{updateId}.msu";
    }

    private string DetermineFileExtension(string title)
    {
        // Analyze title to determine most likely file extension
        var titleLower = title.ToLowerInvariant();
        
        if (titleLower.Contains("security update") || titleLower.Contains("update for"))
        {
            return "msu";
        }
        else if (titleLower.Contains("cumulative update"))
        {
            return "msu";
        }
        else if (titleLower.Contains("feature update"))
        {
            return "esd";
        }
        else if (titleLower.Contains("driver"))
        {
            return "cab";
        }
        else if (titleLower.Contains(".net"))
        {
            return "exe";
        }
        
        // Default to .msu for Windows updates
        return "msu";
    }

    private async Task StoreUpdateMetadataAsync(string updateId, string title, string filename, string filePath)
    {
        var metadata = new
        {
            UpdateId = updateId,
            Title = title,
            Filename = filename,
            FilePath = filePath,
            FolderSuffix = GetFolderSuffix(filename),
            StoredAt = DateTime.UtcNow,
            IpakCompatible = true
        };

        var metadataPath = Path.Combine(this.baseContentPath, "_metadata", $"{updateId}.json");
        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, metadataJson);
    }
}

/// <summary>
/// Extension methods for IPAK filename analysis.
/// </summary>
public static class IpakFilenameExtensions
{
    /// <summary>
    /// Gets examples of how various filenames would be organized in IPAK structure.
    /// </summary>
    public static Dictionary<string, string> GetIpakExamples()
    {
        var examples = new Dictionary<string, string>
        {
            ["windows10.0-kb5000064-x64.msu"] = "UpdateFiles/64/",
            ["kb2267602-x86.msu"] = "UpdateFiles/86/",
            ["ie11-windows6.1-kb4534251.msu"] = "UpdateFiles/51/",
            ["windows-kb5003173-x64.msu"] = "UpdateFiles/64/",
            ["dotnet-framework-kb4533002.exe"] = "UpdateFiles/02/",
            ["driver-update-kb5001234-arm64.cab"] = "UpdateFiles/64/"
        };

        return examples;
    }

    /// <summary>
    /// Validates that a filename follows expected patterns.
    /// </summary>
    public static bool IsValidUpdateFilename(string filename)
    {
        var validExtensions = new[] { ".msu", ".exe", ".cab", ".msi", ".esd" };
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        
        return validExtensions.Contains(extension) && 
               !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(filename));
    }
}