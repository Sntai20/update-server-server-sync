// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

public class AnomalyDetectionResult
{
    public bool IsAnomaly { get; set; }
    public double Score { get; set; }
    public required string Message { get; set; }
}