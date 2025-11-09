// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

public class UpdateMetadata
{
    public string KB_ID { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public float FileSize { get; set; }
    public bool IsSigned { get; set; }
    public string DomainReputation { get; set; } = string.Empty;
    public bool HashMatch { get; set; }
    public float UpdateFrequency { get; set; }
}
