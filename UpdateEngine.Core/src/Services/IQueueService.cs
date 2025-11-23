// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using UpdateEngine.Core.Models;

public interface IQueueService
{
    Task EnqueueAnomalyEventAsync(AnomalyEvent evt);
}