// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.Metadata.Metrics
{
    /// <summary>
    /// Metrics instrumentation for metadata store operations
    /// </summary>
    internal sealed class MetadataStoreMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.MetadataStore", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> QueriesExecuted = s_meter.CreateCounter<long>(
            "metadata_queries_total",
            unit: "{queries}",
            description: "Total number of metadata queries executed");
            
        internal static readonly Counter<long> PackagesAdded = s_meter.CreateCounter<long>(
            "metadata_packages_added",
            unit: "{packages}",
            description: "Total number of packages added to the store");
            
        internal static readonly Counter<long> PackagesRetrieved = s_meter.CreateCounter<long>(
            "metadata_packages_retrieved",
            unit: "{packages}",
            description: "Total number of packages retrieved from the store");
            
        internal static readonly Counter<long> FlushOperations = s_meter.CreateCounter<long>(
            "metadata_flush_operations",
            unit: "{operations}",
            description: "Total number of store flush operations");
            
        internal static readonly Counter<long> ReindexOperations = s_meter.CreateCounter<long>(
            "metadata_reindex_operations",
            unit: "{operations}",
            description: "Total number of store reindex operations");
            
        internal static readonly Counter<long> ReindexOperationsFailed = s_meter.CreateCounter<long>(
            "metadata_reindex_failed",
            unit: "{operations}",
            description: "Total number of failed reindex operations");
            
        // Histograms
        internal static readonly Histogram<double> QueryDuration = s_meter.CreateHistogram<double>(
            "metadata_query_duration",
            unit: "s",
            description: "Duration of metadata query operations");
            
        internal static readonly Histogram<double> FlushDuration = s_meter.CreateHistogram<double>(
            "metadata_flush_duration",
            unit: "s",
            description: "Duration of metadata store flush operations");
            
        internal static readonly Histogram<double> ReindexDuration = s_meter.CreateHistogram<double>(
            "metadata_reindex_duration",
            unit: "s",
            description: "Duration of metadata store reindex operations");
            
        internal static readonly Histogram<long> PackageCount = s_meter.CreateHistogram<long>(
            "metadata_package_count",
            unit: "{packages}",
            description: "Number of packages in the store");
            
        // Observable gauges - backing fields
        private static long s_totalPackages = 0;
        private static int s_pendingPackages = 0;
        private static int s_activeQueries = 0;
            
        // Gauges
        internal static readonly ObservableGauge<long> TotalPackages = s_meter.CreateObservableGauge<long>(
            "metadata_total_packages",
            () => s_totalPackages,
            unit: "{packages}",
            description: "Total number of packages in the metadata store");
            
        internal static readonly ObservableGauge<int> PendingPackages = s_meter.CreateObservableGauge<int>(
            "metadata_pending_packages",
            () => s_pendingPackages,
            unit: "{packages}",
            description: "Number of packages pending indexing");
            
        internal static readonly ObservableGauge<int> ActiveQueries = s_meter.CreateObservableGauge<int>(
            "metadata_active_queries",
            () => s_activeQueries,
            unit: "{queries}",
            description: "Number of currently active queries");

        // Methods to update gauge backing fields
        internal static void SetTotalPackages(long count) => System.Threading.Interlocked.Exchange(ref s_totalPackages, count);
        internal static void SetPendingPackages(int count) => System.Threading.Interlocked.Exchange(ref s_pendingPackages, count);
        internal static void IncrementActiveQueries() => System.Threading.Interlocked.Increment(ref s_activeQueries);
        internal static void DecrementActiveQueries() => System.Threading.Interlocked.Decrement(ref s_activeQueries);
    }
}
