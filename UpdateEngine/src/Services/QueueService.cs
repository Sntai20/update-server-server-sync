// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UpdateEngine.Models;

namespace UpdateEngine.Services;

public class QueueService : IQueueService
{
    private readonly ILogger<QueueService> logger;
    private readonly QueueClient? queueClient;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly bool enabled;

    public QueueService(
        ILogger<QueueService> logger,
        IConfiguration configuration,
        JsonSerializerOptions jsonOptions)
    {
        this.logger = logger;
        this.jsonOptions = jsonOptions;
        
        this.enabled = configuration.GetValue<bool>("Features:EnableAnomalyDetection", false);
        
        if (!this.enabled)
        {
            this.logger.LogInformation("Anomaly detection queue service is disabled");
            return;
        }

        var connectionString = configuration.GetConnectionString("AzureWebJobsStorage") 
            ?? configuration["AzureWebJobsStorage"] 
            ?? "UseDevelopmentStorage=true";
        
        var queueName = configuration["AnomalyDetection:QueueName"] ?? "anomaly-events";
        
        try
        {
            this.queueClient = new QueueClient(connectionString, queueName);
            this.queueClient.CreateIfNotExists();
            this.logger.LogInformation("Anomaly queue service initialized with queue: {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to initialize anomaly queue service");
        }
    }

    public async Task EnqueueAnomalyEventAsync(AnomalyEvent evt)
    {
        if (!this.enabled || this.queueClient == null)
        {
            this.logger.LogDebug("Skipping queue enqueue - service disabled or not initialized");
            return;
        }

        try
        {
            var messageJson = JsonSerializer.Serialize(evt, this.jsonOptions);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
            var base64Message = Convert.ToBase64String(messageBytes);
            
            await this.queueClient.SendMessageAsync(base64Message);
            this.logger.LogInformation("Enqueued anomaly event for KB {KbId}", evt.KB_ID);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to enqueue anomaly event for KB {KbId}", evt.KB_ID);
            throw;
        }
    }
}