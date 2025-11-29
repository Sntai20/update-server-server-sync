// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.Metadata.Storage.Metrics
{
    /// <summary>
    /// Metrics instrumentation for content store operations
    /// </summary>
    internal sealed class ContentStoreMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.ContentStore", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> CacheHits = s_meter.CreateCounter<long>(
            "content_cache_hits",
            unit: "{hits}",
            description: "Total number of content cache hits");
            
        internal static readonly Counter<long> CacheMisses = s_meter.CreateCounter<long>(
            "content_cache_misses",
            unit: "{misses}",
            description: "Total number of content cache misses");
            
        internal static readonly Counter<long> FilesStored = s_meter.CreateCounter<long>(
            "content_files_stored",
            unit: "{files}",
            description: "Total number of files stored in content store");
            
        internal static readonly Counter<long> FilesRetrieved = s_meter.CreateCounter<long>(
            "content_files_retrieved",
            unit: "{files}",
            description: "Total number of files retrieved from content store");
            
        internal static readonly Counter<long> FilesDeleted = s_meter.CreateCounter<long>(
            "content_files_deleted",
            unit: "{files}",
            description: "Total number of files deleted from content store");
            
        internal static readonly Counter<long> StorageOperationsFailed = s_meter.CreateCounter<long>(
            "content_storage_operations_failed",
            unit: "{operations}",
            description: "Total number of failed storage operations");
            
        internal static readonly Counter<long> BytesWritten = s_meter.CreateCounter<long>(
            "content_bytes_written",
            unit: "By",
            description: "Total bytes written to content store");
            
        internal static readonly Counter<long> BytesRead = s_meter.CreateCounter<long>(
            "content_bytes_read",
            unit: "By",
            description: "Total bytes read from content store");
            
        // Histograms
        internal static readonly Histogram<double> StoreDuration = s_meter.CreateHistogram<double>(
            "content_store_duration",
            unit: "s",
            description: "Duration of content store operations");
            
        internal static readonly Histogram<double> RetrieveDuration = s_meter.CreateHistogram<double>(
            "content_retrieve_duration",
            unit: "s",
            description: "Duration of content retrieve operations");
            
        internal static readonly Histogram<long> FileSize = s_meter.CreateHistogram<long>(
            "content_file_size",
            unit: "By",
            description: "Size of files stored/retrieved");
            
        // Observable gauges - backing fields
        private static long s_totalStorageUsed = 0;
        private static long s_totalFilesStored = 0;
        private static double s_cacheHitRate = 0.0;
            
        // Gauges
        internal static readonly ObservableGauge<long> TotalStorageUsed = s_meter.CreateObservableGauge<long>(
            "content_total_storage_used",
            () => s_totalStorageUsed,
            unit: "By",
            description: "Total storage space used by content store");
            
        internal static readonly ObservableGauge<long> TotalFilesStored = s_meter.CreateObservableGauge<long>(
            "content_total_files_stored",
            () => s_totalFilesStored,
            unit: "{files}",
            description: "Total number of files currently in content store");
            
        internal static readonly ObservableGauge<double> CacheHitRate = s_meter.CreateObservableGauge<double>(
            "content_cache_hit_rate",
            () => s_cacheHitRate,
            unit: "{ratio}",
            description: "Cache hit rate as a ratio (0.0 to 1.0)");

        // Methods to update gauge backing fields
        internal static void SetTotalStorageUsed(long bytes) => System.Threading.Interlocked.Exchange(ref s_totalStorageUsed, bytes);
        internal static void AddStorageUsed(long bytes) => System.Threading.Interlocked.Add(ref s_totalStorageUsed, bytes);
        internal static void SetTotalFilesStored(long count) => System.Threading.Interlocked.Exchange(ref s_totalFilesStored, count);
        internal static void IncrementTotalFilesStored() => System.Threading.Interlocked.Increment(ref s_totalFilesStored);
        internal static void DecrementTotalFilesStored() => System.Threading.Interlocked.Decrement(ref s_totalFilesStored);
        internal static void SetCacheHitRate(double rate) => System.Threading.Interlocked.Exchange(ref s_cacheHitRate, rate);
    }
}
