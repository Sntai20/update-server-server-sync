# Advanced Trigger Functions Configuration

The `MetadataSyncFunctionsImproved.cs` file now includes advanced trigger capabilities for enterprise scenarios:

## Available Triggers

### 1. Timer Triggers (Scheduled Sync)
- **DailyMetadataSync**: Runs daily at 2:00 AM UTC
- **WeeklyMetadataSync**: Runs weekly on Sundays at 3:00 AM UTC
- **HourlyMetadataSync**: Runs every hour for high-frequency sync

### 2. Service Bus Trigger
- **ProcessMetadataSyncRequest**: Processes sync requests from Azure Service Bus queue
- Supports complex sync operations with filtering
- Queue name: `metadata-sync-requests`

### 3. Blob Storage Trigger
- **ProcessSyncConfigFile**: Monitors blob container for configuration file changes
- Container: `sync-configs`
- Supports JSON configuration files for batch operations

## Required Configuration

Add these settings to your `local.settings.json` or Azure Function App Configuration:

```json
{
  "Values": {
    "ServiceBusConnection": "Endpoint=sb://your-servicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key",
    "StorageConnection": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=your-key;EndpointSuffix=core.windows.net"
  }
}
```

## Usage Examples

### Service Bus Request Format
Send JSON message to `metadata-sync-requests` queue:
```json
{
  "SyncType": "Updates",
  "UpstreamEndpoint": "https://update.microsoft.com/",
  "ProductsFilter": ["guid1", "guid2"],
  "ClassificationsFilter": ["guid3", "guid4"],
  "MaxItems": 100,
  "SkipSuperseded": true
}
```

### Blob Configuration Format
Upload JSON file to `sync-configs` container:
```json
{
  "Operations": [
    {
      "Type": "Categories",
      "UpstreamEndpoint": "https://update.microsoft.com/",
      "MaxItems": 50
    },
    {
      "Type": "Updates",
      "UpstreamEndpoint": "https://update.microsoft.com/",
      "ProductsFilter": ["guid1", "guid2"],
      "MaxItems": 100
    }
  ]
}
```

## Benefits

1. **Automation**: Scheduled sync operations without manual intervention
2. **Scalability**: Event-driven processing through Service Bus
3. **Flexibility**: Configuration-driven batch operations via blob storage
4. **Monitoring**: Comprehensive logging for all trigger operations
5. **Error Handling**: Robust error handling with detailed logging

## Dependencies Added

- Microsoft.Azure.Functions.Worker.Extensions.Timer (4.3.0)
- Microsoft.Azure.Functions.Worker.Extensions.ServiceBus (5.16.0)
- Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs (6.2.0)

All advanced trigger functions are now successfully integrated and building without errors.