// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

/// <summary>
/// Represents an anomaly event for Windows Update metadata.
/// </summary>
public class AnomalyEvent
{
    public string KB_ID { get; set; } = string.Empty;
    public bool HashMatch { get; set; }
    public double Score { get; set; }
    public bool IsSigned { get; set; }
    public double DomainReputation { get; set; }
}