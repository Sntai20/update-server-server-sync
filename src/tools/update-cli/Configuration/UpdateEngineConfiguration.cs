// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateCli.Configuration;

/// <summary>
/// Configuration for the UpdateEngine API endpoint.
/// </summary>
public class UpdateEngineConfiguration
{
    /// <summary>Gets or sets the base URL for the UpdateEngine API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:7071";

    /// <summary>Gets or sets the timeout for HTTP requests.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}