// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.UpstreamSource.Metrics
{
    /// <summary>
    /// Metrics instrumentation for upstream source HTTP operations
    /// </summary>
    internal sealed class UpstreamSourceMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.UpstreamSource", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> RequestsStarted = s_meter.CreateCounter<long>(
            "upstream_requests_started",
            unit: "{requests}",
            description: "Total number of upstream requests started");
            
        internal static readonly Counter<long> RequestsCompleted = s_meter.CreateCounter<long>(
            "upstream_requests_completed",
            unit: "{requests}",
            description: "Total number of upstream requests completed successfully");
            
        internal static readonly Counter<long> RequestsFailed = s_meter.CreateCounter<long>(
            "upstream_requests_failed",
            unit: "{requests}",
            description: "Total number of upstream requests that failed");
            
        internal static readonly Counter<long> RequestsRetried = s_meter.CreateCounter<long>(
            "upstream_requests_retried",
            unit: "{requests}",
            description: "Total number of upstream requests retried");
            
        internal static readonly Counter<long> BytesReceived = s_meter.CreateCounter<long>(
            "upstream_bytes_received",
            unit: "By",
            description: "Total bytes received from upstream source");
            
        internal static readonly Counter<long> MetadataPackagesReceived = s_meter.CreateCounter<long>(
            "upstream_metadata_packages_received",
            unit: "{packages}",
            description: "Total number of metadata packages received from upstream");
            
        internal static readonly Counter<long> CategoriesReceived = s_meter.CreateCounter<long>(
            "upstream_categories_received",
            unit: "{categories}",
            description: "Total number of categories received from upstream");
            
        internal static readonly Counter<long> UpdatesReceived = s_meter.CreateCounter<long>(
            "upstream_updates_received",
            unit: "{updates}",
            description: "Total number of updates received from upstream");
            
        // Histograms
        internal static readonly Histogram<double> RequestDuration = s_meter.CreateHistogram<double>(
            "upstream_request_duration",
            unit: "s",
            description: "Duration of upstream HTTP requests");
            
        internal static readonly Histogram<long> ResponseSize = s_meter.CreateHistogram<long>(
            "upstream_response_size",
            unit: "By",
            description: "Size of upstream HTTP responses");
            
        internal static readonly Histogram<long> MetadataCopyBatchSize = s_meter.CreateHistogram<long>(
            "upstream_metadata_batch_size",
            unit: "{packages}",
            description: "Number of packages in metadata copy batch operations");
            
        // Observable gauges - backing fields
        private static int s_activeRequests = 0;
        private static int s_connectionPoolSize = 0;
            
        // Gauges
        internal static readonly ObservableGauge<int> ActiveRequests = s_meter.CreateObservableGauge<int>(
            "upstream_active_requests",
            () => s_activeRequests,
            unit: "{requests}",
            description: "Number of currently active upstream requests");
            
        internal static readonly ObservableGauge<int> ConnectionPoolSize = s_meter.CreateObservableGauge<int>(
            "upstream_connection_pool_size",
            () => s_connectionPoolSize,
            unit: "{connections}",
            description: "Number of connections in the HTTP connection pool");

        // Methods to update gauge backing fields
        internal static void IncrementActiveRequests() => System.Threading.Interlocked.Increment(ref s_activeRequests);
        internal static void DecrementActiveRequests() => System.Threading.Interlocked.Decrement(ref s_activeRequests);
        internal static void SetConnectionPoolSize(int size) => System.Threading.Interlocked.Exchange(ref s_connectionPoolSize, size);
    }
}
