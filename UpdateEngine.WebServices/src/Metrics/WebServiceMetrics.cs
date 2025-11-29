// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.WebServices.Metrics
{
    /// <summary>
    /// Metrics instrumentation for web service operations
    /// </summary>
    internal sealed class WebServiceMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.WebService", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> RequestsReceived = s_meter.CreateCounter<long>(
            "webservice_requests_received",
            unit: "{requests}",
            description: "Total number of HTTP requests received");
            
        internal static readonly Counter<long> RequestsSuccessful = s_meter.CreateCounter<long>(
            "webservice_requests_successful",
            unit: "{requests}",
            description: "Total number of successful HTTP requests");
            
        internal static readonly Counter<long> RequestsFailed = s_meter.CreateCounter<long>(
            "webservice_requests_failed",
            unit: "{requests}",
            description: "Total number of failed HTTP requests");
            
        internal static readonly Counter<long> RequestsByEndpoint = s_meter.CreateCounter<long>(
            "webservice_requests_by_endpoint",
            unit: "{requests}",
            description: "Total number of requests grouped by endpoint");
            
        internal static readonly Counter<long> ResponsesSent = s_meter.CreateCounter<long>(
            "webservice_responses_sent",
            unit: "{responses}",
            description: "Total number of HTTP responses sent");
            
        internal static readonly Counter<long> BytesSent = s_meter.CreateCounter<long>(
            "webservice_bytes_sent",
            unit: "By",
            description: "Total bytes sent in HTTP responses");
            
        internal static readonly Counter<long> BytesReceived = s_meter.CreateCounter<long>(
            "webservice_bytes_received",
            unit: "By",
            description: "Total bytes received in HTTP requests");
            
        // Histograms
        internal static readonly Histogram<double> RequestDuration = s_meter.CreateHistogram<double>(
            "webservice_request_duration",
            unit: "s",
            description: "Duration of HTTP request processing");
            
        internal static readonly Histogram<long> RequestSize = s_meter.CreateHistogram<long>(
            "webservice_request_size",
            unit: "By",
            description: "Size of HTTP request bodies");
            
        internal static readonly Histogram<long> ResponseSize = s_meter.CreateHistogram<long>(
            "webservice_response_size",
            unit: "By",
            description: "Size of HTTP response bodies");
            
        internal static readonly Histogram<int> HttpStatusCode = s_meter.CreateHistogram<int>(
            "webservice_http_status_code",
            unit: "{code}",
            description: "HTTP status codes returned");
            
        // Observable gauges - backing fields
        private static int s_activeRequests = 0;
        private static int s_queuedRequests = 0;
        private static long s_totalRequestsProcessed = 0;
            
        // Gauges
        internal static readonly ObservableGauge<int> ActiveRequests = s_meter.CreateObservableGauge<int>(
            "webservice_active_requests",
            () => s_activeRequests,
            unit: "{requests}",
            description: "Number of currently active HTTP requests");
            
        internal static readonly ObservableGauge<int> QueuedRequests = s_meter.CreateObservableGauge<int>(
            "webservice_queued_requests",
            () => s_queuedRequests,
            unit: "{requests}",
            description: "Number of queued HTTP requests waiting to be processed");
            
        internal static readonly ObservableGauge<long> TotalRequestsProcessed = s_meter.CreateObservableGauge<long>(
            "webservice_total_requests_processed",
            () => s_totalRequestsProcessed,
            unit: "{requests}",
            description: "Total number of requests processed since startup");

        // Methods to update gauge backing fields
        internal static void IncrementActiveRequests() => System.Threading.Interlocked.Increment(ref s_activeRequests);
        internal static void DecrementActiveRequests() => System.Threading.Interlocked.Decrement(ref s_activeRequests);
        internal static void IncrementQueuedRequests() => System.Threading.Interlocked.Increment(ref s_queuedRequests);
        internal static void DecrementQueuedRequests() => System.Threading.Interlocked.Decrement(ref s_queuedRequests);
        internal static void IncrementTotalRequestsProcessed() => System.Threading.Interlocked.Increment(ref s_totalRequestsProcessed);
    }
}
