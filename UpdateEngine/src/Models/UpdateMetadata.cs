// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

/// <summary>
/// UpdateMetadata model for ML.NET anomaly detection
/// Enhanced to leverage Microsoft Update library capabilities
/// </summary>
public class UpdateMetadata
{
    public string KB_ID { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public float FileSize { get; set; }
    public bool IsSigned { get; set; }
    public string DomainReputation { get; set; } = string.Empty;
    public bool HashMatch { get; set; }
    public float UpdateFrequency { get; set; }
    
    // Additional fields leveraging Microsoft Update library
    public int SupersededCount { get; set; } // Number of updates this supersedes
    public int SupersededByCount { get; set; } // Number of updates superseding this
    public int BundledUpdatesCount { get; set; } // Number of bundled updates
    public bool IsSecurityUpdate { get; set; } // Determined from title/classification
    public bool IsCriticalUpdate { get; set; } // Determined from title/classification
    public bool IsCumulativeUpdate { get; set; } // Determined from title
    public string Classification { get; set; } = string.Empty; // Update classification
    public string Product { get; set; } = string.Empty; // Product category
    public int ApplicabilityRulesCount { get; set; } // Number of applicability rules
    public bool HasComplexApplicability { get; set; } // Complex applicability requirements
}
