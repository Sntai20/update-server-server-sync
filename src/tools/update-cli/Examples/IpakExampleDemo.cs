// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateCli.Storage;

namespace UpdateCli.Examples;

/// <summary>
/// Example demonstrating IPAK-compatible filename generation and folder organization.
/// </summary>
public static class IpakExampleDemo
{
    /// <summary>
    /// Demonstrates how various update titles would be organized in IPAK structure.
    /// </summary>
    public static void ShowIpakOrganizationExamples()
    {
        Console.WriteLine("=== IPAK-Compatible File Organization Examples ===");
        Console.WriteLine();

        var examples = new[]
        {
            ("2023-05 Cumulative Update for Windows Server 2022 (KB5000064) x64", "kb5000064-x64.msu"),
            ("Security Update for Windows Server 2022 (KB2267602) x86", "kb2267602-x86.msu"),
            ("Internet Explorer 11 Security Update (KB4534251)", "ie11-kb4534251.msu"),
            (".NET Framework Security Update (KB5003173)", "dotnet-kb5003173.exe"),
            ("Windows Defender Update (KB5001234) arm64", "kb5001234-arm64.cab")
        };

        foreach (var (title, filename) in examples)
        {
            var folderSuffix = IpakCompatibleContentStore.GetFolderSuffix(filename);
            var targetPath = $"UpdateFiles/{folderSuffix}/{filename}";
            
            Console.WriteLine($"Title: {title}");
            Console.WriteLine($"Generated Filename: {filename}");
            Console.WriteLine($"IPAK Storage Path: {targetPath}");
            Console.WriteLine($"Folder Suffix: {folderSuffix}");
            Console.WriteLine();
        }

        Console.WriteLine("=== IPAK Directory Structure ===");
        Console.WriteLine();
        Console.WriteLine("C:/Updates/");
        Console.WriteLine("├── UpdateFiles/");
        Console.WriteLine("│   ├── 64/               # kb5000064-x64.msu");
        Console.WriteLine("│   ├── 86/               # kb2267602-x86.msu");
        Console.WriteLine("│   ├── 51/               # ie11-kb4534251.msu");
        Console.WriteLine("│   ├── 73/               # dotnet-kb5003173.exe");
        Console.WriteLine("│   └── 64/               # kb5001234-arm64.cab");
        Console.WriteLine("├── CustomUpdates/");
        Console.WriteLine("├── WUAgent/");
        Console.WriteLine("├── _metadata/            # CLI metadata");
        Console.WriteLine("├── metadata/             # Standard metadata");
        Console.WriteLine("└── logs/                 # Download logs");
    }

    /// <summary>
    /// Validates IPAK filename generation rules.
    /// </summary>
    public static bool ValidateIpakRules()
    {
        var testCases = new Dictionary<string, string>
        {
            ["kb5000064-x64.msu"] = "64",
            ["kb2267602-x86.msu"] = "86", 
            ["ie11-kb4534251.msu"] = "51",
            ["dotnet-kb5003173.exe"] = "73",
            ["driver-kb5001234-arm64.cab"] = "64"
        };

        Console.WriteLine("=== IPAK Rules Validation ===");
        Console.WriteLine();

        var allValid = true;
        foreach (var (filename, expectedSuffix) in testCases)
        {
            var actualSuffix = IpakCompatibleContentStore.GetFolderSuffix(filename);
            var isValid = actualSuffix == expectedSuffix;
            allValid &= isValid;
            
            var status = isValid ? "✓" : "✗";
            Console.WriteLine($"{status} {filename} → {actualSuffix} (expected: {expectedSuffix})");
        }

        Console.WriteLine();
        Console.WriteLine($"Overall validation: {(allValid ? "✓ PASSED" : "✗ FAILED")}");
        
        return allValid;
    }
}