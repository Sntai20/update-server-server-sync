# Language Configuration Guide for Microsoft Update Server-Server Sync

## Overview

This guide explains how to configure supported languages for downloading updates in the Microsoft Update Server-Server Sync project. The language filtering system allows you to specify which language variants of updates to download, helping to reduce storage requirements and sync only the content you need.

## Supported Language Formats

The language filtering system supports multiple language identifier formats:

### Standard Language Codes
- `"en"` - Generic English
- `"en-US"` - English (United States)
- `"en-GB"` - English (Great Britain)
- `"de"` - German
- `"fr"` - French
- `"es"` - Spanish
- `"ja"` - Japanese
- `"ko"` - Korean
- `"zh-CN"` - Chinese (Simplified)
- `"zh-TW"` - Chinese (Traditional)

### Special Language Values
- `"neutral"` - Language-neutral updates (applicable to all languages)
- `""` (empty string) - Updates without specific language requirements
- `"*"` - All languages (if you want to download everything)

## Configuration Methods

### Method 1: AppHost Configuration (Recommended)

Configure default supported languages in your AppHost `appsettings.json` files:

```json
{
  "UpdateServer": {
    "ServiceUrl": "http://localhost:7071",
    "ContentUrl": "http://localhost:7071/api/content",
    "MaxUpdateCount": 5,
    "SupportedCategories": ["Security Updates", "Critical Updates", "Definition Updates"],
    "SupportedProducts": ["Windows 11", "Windows 10"],
    "SupportedLanguages": ["en", "en-US", "neutral", ""]
  }
}
```

#### Development Environment (`appsettings.Development.json`)
For English and neutral language support (current default):

```json
"SupportedLanguages": ["en", "en-US", "neutral", ""]
```

#### Production Environment (`appsettings.Production.json`)
For multi-language environments:

```json
"SupportedLanguages": ["en", "en-US", "de", "fr", "neutral", ""]
```

### Method 2: HTTP API Configuration

#### Using the Universal Sync Endpoint
The existing `/api/UniversalSync` endpoint now automatically includes English and neutral language filtering.

#### Using the Language-Specific Endpoint
Use the new `/api/SyncWithLanguageFilter` endpoint for custom language configuration:

```http
POST /api/SyncWithLanguageFilter
Content-Type: application/json

{
  "syncType": "critical",
  "syncContent": true,
  "languageFilters": ["en", "en-US", "neutral", ""],
  "productFilters": ["Windows 11"],
  "classificationFilters": ["Security Updates", "Critical Updates"],
  "contentDaysBack": 30
}
```

#### Windows 11 Specialized Endpoint
The `/api/SyncWindows11Critical` endpoint automatically includes English and neutral language filtering for Windows 11 critical updates.

## Current Implementation

### Default Language Configuration

By default, the system is configured to download:

1. **English Updates**: `"en"`, `"en-US"`
2. **Language-Neutral Updates**: `"neutral"`, `""`

This configuration covers most English-language environments while including universal updates that apply to all languages.

### Automatic Language Filtering

Language filters are automatically applied to:

1. **Critical Content Sync**: Windows 11 security and critical updates
2. **Regular Content Sync**: All content downloads
3. **Comprehensive Sync**: Content portion of full synchronization
4. **Custom Sync Operations**: When using the language-filter endpoint

### Functions with Language Support

#### Enhanced Functions
- `PerformCriticalContentSync()` - Now includes English + neutral filtering
- `PerformContentSync()` - Now includes language filtering
- `SyncWindows11Critical` endpoint - Automatic language filtering
- New `SyncWithLanguageFilter` endpoint - Custom language configuration

#### Helper Methods
- `PerformContentSyncWithLanguageFilter()` - Custom language filtering for content
- `PerformCriticalContentSyncWithLanguageFilter()` - Custom language filtering for critical content

## Usage Examples

### Example 1: Download Only English Content

```json
{
  "syncType": "critical",
  "languageFilters": ["en", "en-US"],
  "productFilters": ["Windows 11"],
  "classificationFilters": ["Security Updates", "Critical Updates"]
}
```

### Example 2: Multi-Language European Environment

```json
{
  "syncType": "comprehensive",
  "languageFilters": ["en", "en-US", "de", "fr", "es", "neutral", ""],
  "productFilters": ["Windows 11", "Windows 10"],
  "classificationFilters": ["Security Updates", "Critical Updates", "Updates"]
}
```

### Example 3: Asian Languages + English

```json
{
  "syncType": "content",
  "languageFilters": ["en", "en-US", "ja", "ko", "zh-CN", "neutral", ""],
  "productFilters": ["Windows 11"],
  "contentDaysBack": 60
}
```

### Example 4: Neutral and Universal Only

```json
{
  "syncType": "critical",
  "languageFilters": ["neutral", ""],
  "productFilters": ["Windows 11"],
  "classificationFilters": ["Security Updates", "Critical Updates"]
}
```

## Configuration Tips

### Storage Optimization
- **Minimal Setup**: `["neutral", ""]` - Only language-neutral updates
- **English Only**: `["en", "en-US", "neutral", ""]` - English + universal
- **Multi-Language**: Add specific language codes as needed

### Performance Considerations
- **Fewer Languages = Faster Sync**: More language filters mean more content to evaluate and download
- **Regular Monitoring**: Check storage usage when adding new languages
- **Incremental Addition**: Start with essential languages and add more as needed

### Language Priority
1. Always include `"neutral"` and `""` for universal updates
2. Add your primary language (e.g., `"en"` for English)
3. Add region-specific variants if needed (e.g., `"en-US"`)
4. Add additional languages based on your user base

## Testing Language Configuration

### Verify Language Filtering
1. Start AppHost: `cd UpdateEngine.AppHost/src && dotnet run`
2. Trigger sync with language filters:
   ```bash
   curl -X POST http://localhost:7071/api/SyncWithLanguageFilter \
     -H "Content-Type: application/json" \
     -d '{"syncType":"critical","languageFilters":["en","neutral",""]}'
   ```

### Monitor Download Results
Check the logs for language filter confirmation:
```
Language-filtered content sync filter: Products=[Windows 11], Classifications=[Security Updates, Critical Updates], Languages=[en, en-US, neutral, ], UpdatedAfter=...
```

### Verify Content Storage
Check if content files are being downloaded to your content storage location:
- **Local**: `./data/Content/`
- **Azure**: Check the content container in Azure Storage

## Troubleshooting

### Common Issues

#### No Content Downloaded
- **Check Language Filters**: Ensure you're including universal languages (`"neutral"`, `""`)
- **Verify Product Filters**: Make sure you're targeting the right products (Windows 11, Windows 10)
- **Content Store Configuration**: Verify content storage is properly configured

#### Too Much Content Downloaded
- **Refine Language Filters**: Remove unnecessary language codes
- **Adjust Time Filters**: Reduce `contentDaysBack` parameter
- **Filter by Classification**: Only sync critical classifications

#### Missing Updates for Specific Languages
- **Check Language Code Format**: Use correct format (`"en-US"` vs `"en_US"`)
- **Include Universal Updates**: Always include `"neutral"` and `""`
- **Verify Update Availability**: Some updates may not be available in all languages

### Debug Language Filtering

Enable detailed logging to see filter application:
```json
{
  "Logging": {
    "LogLevel": {
      "UpdateEngine.Functions.UnifiedSyncFunctions": "Information",
      "UpdateEngine.Services.SyncService": "Information"
    }
  }
}
```

## Migration from Previous Versions

### Upgrading Existing Installations

If you're upgrading from a version without language support:

1. **Update Configuration**: Add `SupportedLanguages` to your `appsettings.json`
2. **Reindex if Needed**: The system will automatically handle language filtering going forward
3. **Monitor First Sync**: Watch the first sync after upgrade to ensure proper filtering

### Backward Compatibility

- Existing sync operations continue to work unchanged
- Language filtering is additive - no existing functionality is removed
- Default behavior includes English and neutral languages

## Advanced Configuration

### Custom Language Filter Implementation

For advanced scenarios, you can extend the `ServiceMetadataFilter` class:

```csharp
var filter = new ServiceMetadataFilter
{
    ProductFilters = new List<string> { "Windows 11" },
    ClassificationFilters = new List<string> { "Security Updates" },
    LanguageFilters = GetDynamicLanguageFilters(), // Custom logic
    UpdatedAfter = DateTime.UtcNow.AddDays(-30)
};
```

### Environment-Specific Configuration

Use different language configurations per environment:

```json
// Development
"SupportedLanguages": ["en", "neutral", ""]

// Staging
"SupportedLanguages": ["en", "en-US", "fr", "neutral", ""]

// Production
"SupportedLanguages": ["en", "en-US", "de", "fr", "es", "ja", "neutral", ""]
```

## Related Documentation

- [Storage Configuration Guide](STORAGE_GUIDE.md) - Configure content storage
- [Sync Operations Guide](SYNC_OPERATIONS_GUIDE.md) - Understanding sync types
- [API Reference](../api/) - Complete API documentation
- [Troubleshooting Guide](TROUBLESHOOTING_STORAGE.md) - Common issues and solutions

---

**Last Updated**: 2025-01-16  
**Version**: 1.0  
**Applies To**: .NET 9.0, Azure Functions v4, UpdateEngine