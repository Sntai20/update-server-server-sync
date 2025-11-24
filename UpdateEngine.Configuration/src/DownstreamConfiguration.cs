// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Configuration;

/// <summary>
/// Configuration for downstream synchronization from upstream UpdateEngine Functions.
/// Enables WorkerService to pull metadata/content from Functions instead of Microsoft Update.
/// </summary>
public class DownstreamConfiguration
{
    /// <summary>
    /// Base URL of the upstream UpdateEngine Functions instance.
    /// Example: "http://localhost:7071" or "https://myupdateengine.azurewebsites.net"
    /// </summary>
    public string UpstreamFunctionsUrl { get; set; } = "http://localhost:7071";

    /// <summary>
    /// If true, sync metadata and content from upstream Functions instead of Microsoft Update.
    /// If false, sync directly from Microsoft Update (default behavior).
    /// </summary>
    public bool SyncFromUpstream { get; set; } = false;

    /// <summary>
    /// Interval (in minutes) between downstream sync operations.
    /// Default: 60 minutes.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// If true, also sync content files from upstream (in addition to metadata).
    /// If false, only sync metadata.
    /// </summary>
    public bool EnableContentSync { get; set; } = true;

    /// <summary>
    /// HTTP client timeout for requests to upstream Functions.
    /// Default: 10 minutes to handle large metadata exports.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum number of retries for failed HTTP requests to upstream.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries (exponential backoff will be applied).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (this.SyncFromUpstream)
        {
            if (string.IsNullOrWhiteSpace(this.UpstreamFunctionsUrl))
            {
                throw new InvalidOperationException("UpstreamFunctionsUrl is required when SyncFromUpstream is enabled");
            }

            if (!Uri.TryCreate(this.UpstreamFunctionsUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Invalid UpstreamFunctionsUrl: {this.UpstreamFunctionsUrl}");
            }

            if (this.SyncIntervalMinutes <= 0)
            {
                throw new InvalidOperationException("SyncIntervalMinutes must be greater than 0");
            }

            if (this.HttpTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("HttpTimeout must be greater than zero");
            }

            if (this.MaxRetries < 0)
            {
                throw new InvalidOperationException("MaxRetries must be greater than or equal to 0");
            }
        }
    }
}
