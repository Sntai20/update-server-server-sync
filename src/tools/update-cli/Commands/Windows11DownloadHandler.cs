// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateCli.Services;

namespace UpdateCli.Commands;

/// <summary>
/// Windows 11 specific download handler.
/// </summary>
public class Windows11DownloadHandler : BaseWindowsDownloadHandler
{
    public Windows11DownloadHandler(UpdateEngineClient updateEngineClient) 
        : base(updateEngineClient)
    {
    }

    /// <summary>
    /// Gets the search terms for Windows 11.
    /// </summary>
    protected override string[] GetSearchTerms(bool securityOnly)
    {
        return securityOnly 
            ? new[] { "Security Updates", "Windows 11" }
            : new[] { "Security Updates", "Critical Updates", "Update Rollups", "Windows 11", "Feature Updates", "Quality Updates" };
    }

    /// <summary>
    /// Gets the display name for Windows 11.
    /// </summary>
    protected override string GetDisplayName()
    {
        return "Windows 11";
    }
}