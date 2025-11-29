// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.Metadata.Storage.Azure
{
    /// <summary>
    /// Metrics instrumentation for <see cref="BlobContentStore"/> operations
    /// </summary>
    internal sealed class BlobContentStoreMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.BlobContentStore", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> DownloadsStarted = s_meter.CreateCounter<long>(
            "blob_content_downloads_started",
            unit: "{downloads}",
            description: "Total number of content downloads started");
            
        internal static readonly Counter<long> DownloadsCompleted = s_meter.CreateCounter<long>(
            "blob_content_downloads_completed",
            unit: "{downloads}",
            description: "Total number of content downloads completed successfully");
            
        internal static readonly Counter<long> DownloadsFailed = s_meter.CreateCounter<long>(
            "blob_content_downloads_failed",
            unit: "{downloads}",
            description: "Total number of content downloads that failed");
            
        internal static readonly Counter<long> BytesDownloaded = s_meter.CreateCounter<long>(
            "blob_content_bytes_downloaded",
            unit: "By",
            description: "Total bytes downloaded from source");
            
        internal static readonly Counter<long> BytesStaged = s_meter.CreateCounter<long>(
            "blob_content_bytes_staged",
            unit: "By",
            description: "Total bytes staged to Azure Blob Storage");
            
        internal static readonly Counter<long> BlocksStaged = s_meter.CreateCounter<long>(
            "blob_content_blocks_staged",
            unit: "{blocks}",
            description: "Total number of blocks staged to Azure Blob Storage");
            
        internal static readonly Counter<long> BlockRetriesTotal = s_meter.CreateCounter<long>(
            "blob_content_block_retries",
            unit: "{retries}",
            description: "Total number of block download retries");
            
        // Histograms
        internal static readonly Histogram<double> DownloadDuration = s_meter.CreateHistogram<double>(
            "blob_content_download_duration",
            unit: "s",
            description: "Duration of content file downloads");
            
        internal static readonly Histogram<double> BlockDownloadDuration = s_meter.CreateHistogram<double>(
            "blob_content_block_download_duration",
            unit: "s",
            description: "Duration of individual block downloads");
            
        internal static readonly Histogram<long> BlockSize = s_meter.CreateHistogram<long>(
            "blob_content_block_size",
            unit: "By",
            description: "Size of downloaded blocks");
            
        // Observable gauges - backing fields
        private static int s_activeDownloadCount = 0;
        private static long s_queuedBytesGauge = 0;
            
        // Gauges (ObservableGauges)
        internal static readonly ObservableGauge<int> ActiveDownloads = s_meter.CreateObservableGauge<int>(
            "blob_content_active_downloads",
            () => s_activeDownloadCount,
            unit: "{downloads}",
            description: "Number of currently active downloads");
            
        internal static readonly ObservableGauge<long> QueuedBytes = s_meter.CreateObservableGauge<long>(
            "blob_content_queued_bytes",
            () => s_queuedBytesGauge,
            unit: "By",
            description: "Total bytes queued for download");

        // Methods to update gauge backing fields
        internal static void IncrementActiveDownloads() => System.Threading.Interlocked.Increment(ref s_activeDownloadCount);
        internal static void DecrementActiveDownloads() => System.Threading.Interlocked.Decrement(ref s_activeDownloadCount);
        internal static void AddQueuedBytes(long bytes) => System.Threading.Interlocked.Add(ref s_queuedBytesGauge, bytes);
    }
}
