// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using UpdateEngine.Models;

public interface IQueueService
{
    Task EnqueueAnomalyEventAsync(AnomalyEvent evt);
}