// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

/// <summary>
/// Service-level configuration settings.
/// Supports hot-reload via IOptionsMonitor.
/// </summary>
public class ServiceConfiguration
{
    public string ServiceUrl { get; set; } = "http://localhost:7071";
    public string ContentUrl { get; set; } = "http://localhost:7071/api/content";
    public string UpstreamServerUrl { get; set; } = "https://fe2.update.microsoft.com/v6/windowsupdate/Services/";
    public int MaxConcurrentSyncs { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public int MaxUpdateCount { get; set; } = 1000;
    public string[] SupportedCategories { get; set; } = new[] { "Security Updates", "Critical Updates", "Updates" };
    public string[] SupportedLanguages { get; set; } = new[] { "en", "en-US", "neutral", "" };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.ServiceUrl))
        {
            throw new InvalidOperationException("ServiceConfiguration.ServiceUrl is required");
        }

        if (this.MaxConcurrentSyncs < 1)
        {
            throw new InvalidOperationException("ServiceConfiguration.MaxConcurrentSyncs must be at least 1");
        }

        if (this.RequestTimeoutSeconds < 1)
        {
            throw new InvalidOperationException("ServiceConfiguration.RequestTimeoutSeconds must be at least 1");
        }
    }
}
