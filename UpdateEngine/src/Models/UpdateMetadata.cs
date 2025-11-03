// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

public class UpdateMetadata
{
    public float FileSize { get; set; }
    public bool IsSigned { get; set; }
    public float DomainReputation { get; set; }
    public bool HashMatch { get; set; }
    public float UpdateFrequency { get; set; }
}
