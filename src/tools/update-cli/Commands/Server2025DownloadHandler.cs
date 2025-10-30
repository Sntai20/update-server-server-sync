// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateCli.Services;

namespace UpdateCli.Commands;

/// <summary>
/// Windows Server 2025 specific download handler.
/// </summary>
public class Server2025DownloadHandler : BaseWindowsDownloadHandler
{
    public Server2025DownloadHandler(UpdateEngineClient updateEngineClient) 
        : base(updateEngineClient)
    {
    }

    /// <summary>
    /// Gets the search terms for Windows Server 2025.
    /// </summary>
    protected override string[] GetSearchTerms(bool securityOnly)
    {
        return securityOnly 
            ? new[] { "Security Updates", "Windows Server 2025" }
            : new[] { "Security Updates", "Critical Updates", "Update Rollups", "Windows Server 2025", "Feature Updates" };
    }

    /// <summary>
    /// Gets the display name for Windows Server 2025.
    /// </summary>
    protected override string GetDisplayName()
    {
        return "Windows Server 2025";
    }
}