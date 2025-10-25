// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AppHost;

/// <summary>
/// Helper for setting up storage directories in the AppHost.
/// </summary>
public static class StorageSetupHelper
{
    /// <summary>
    /// Ensures that local storage directories exist.
    /// </summary>
    public static void EnsureLocalDirectories(string metadataPath, string? contentPath)
    {
        EnsureDirectory(metadataPath, "metadata");

        if (!string.IsNullOrEmpty(contentPath))
        {
            EnsureDirectory(contentPath, "content");
        }
    }

    /// <summary>
    /// Validates that a local storage path exists.
    /// </summary>
    public static bool ValidateLocalStorage(string path)
    {
        return Directory.Exists(path);
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    private static void EnsureDirectory(string path, string description)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Creating {description} directory: {path}");
            Directory.CreateDirectory(path);
        }
        else
        {
            Console.WriteLine($"{description} directory already exists: {path}");
        }
    }
}
