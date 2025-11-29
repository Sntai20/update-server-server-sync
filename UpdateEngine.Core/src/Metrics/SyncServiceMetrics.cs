// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace UpdateEngine.Core.Metrics
{
    /// <summary>
    /// Metrics instrumentation for <see cref="Services.SyncService"/> operations
    /// </summary>
    internal sealed class SyncServiceMetrics
    {
        private static readonly Meter s_meter = new("UpdateEngine.SyncService", "1.0.0");
        
        // Counters
        internal static readonly Counter<long> CategorySyncsStarted = s_meter.CreateCounter<long>(
            "sync_categories_started",
            unit: "{operations}",
            description: "Total number of category synchronizations started");
            
        internal static readonly Counter<long> CategorySyncsCompleted = s_meter.CreateCounter<long>(
            "sync_categories_completed",
            unit: "{operations}",
            description: "Total number of category synchronizations completed successfully");
            
        internal static readonly Counter<long> CategorySyncsFailed = s_meter.CreateCounter<long>(
            "sync_categories_failed",
            unit: "{operations}",
            description: "Total number of category synchronizations that failed");
            
        internal static readonly Counter<long> UpdateSyncsStarted = s_meter.CreateCounter<long>(
            "sync_updates_started",
            unit: "{operations}",
            description: "Total number of update synchronizations started");
            
        internal static readonly Counter<long> UpdateSyncsCompleted = s_meter.CreateCounter<long>(
            "sync_updates_completed",
            unit: "{operations}",
            description: "Total number of update synchronizations completed successfully");
            
        internal static readonly Counter<long> UpdateSyncsFailed = s_meter.CreateCounter<long>(
            "sync_updates_failed",
            unit: "{operations}",
            description: "Total number of update synchronizations that failed");
            
        internal static readonly Counter<long> ContentSyncsStarted = s_meter.CreateCounter<long>(
            "sync_content_started",
            unit: "{operations}",
            description: "Total number of content synchronizations started");
            
        internal static readonly Counter<long> ContentSyncsCompleted = s_meter.CreateCounter<long>(
            "sync_content_completed",
            unit: "{operations}",
            description: "Total number of content synchronizations completed successfully");
            
        internal static readonly Counter<long> ContentSyncsFailed = s_meter.CreateCounter<long>(
            "sync_content_failed",
            unit: "{operations}",
            description: "Total number of content synchronizations that failed");
            
        internal static readonly Counter<long> UpdatesProcessed = s_meter.CreateCounter<long>(
            "sync_updates_processed",
            unit: "{updates}",
            description: "Total number of updates processed during synchronization");
            
        // Histograms
        internal static readonly Histogram<double> CategorySyncDuration = s_meter.CreateHistogram<double>(
            "sync_categories_duration",
            unit: "s",
            description: "Duration of category synchronization operations");
            
        internal static readonly Histogram<double> UpdateSyncDuration = s_meter.CreateHistogram<double>(
            "sync_updates_duration",
            unit: "s",
            description: "Duration of update synchronization operations");
            
        internal static readonly Histogram<double> ContentSyncDuration = s_meter.CreateHistogram<double>(
            "sync_content_duration",
            unit: "s",
            description: "Duration of content synchronization operations");
            
        internal static readonly Histogram<long> ContentFilesCount = s_meter.CreateHistogram<long>(
            "sync_content_files",
            unit: "{files}",
            description: "Number of files processed during content synchronization");
    }
}
