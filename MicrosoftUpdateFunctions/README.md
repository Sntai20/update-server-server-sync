# Microsoft Update Functions - Service Layer Architecture

A comprehensive Azure Functions implementation of the Microsoft Update Server-Server sync protocol using .NET 8.0 and a modern service layer architecture.

## 🏗️ Architecture Overview

This project implements a **service layer pattern** that separates business logic from Azure Functions infrastructure:

```
HTTP/Timer/ServiceBus Triggers → Service Layer → Storage Layer → Microsoft Update
                                      ↓
                              ISyncService, IQueryService, IHealthService
```

### Core Services

- **ISyncService**: Handles metadata and content synchronization operations
- **IQueryService**: Manages metadata queries, exports, and driver matching  
- **IHealthService**: Provides comprehensive system health monitoring

### Benefits

✅ **Testable**: Business logic separated from Azure Functions infrastructure  
✅ **Maintainable**: Single responsibility principle with clean interfaces  
✅ **Reusable**: Services work across HTTP, Timer, and ServiceBus triggers  
✅ **Scalable**: Automatic scaling with appropriate trigger types  

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure Functions Core Tools 4.x
- Docker (for storage emulator)

### Development Setup

1. **Clone and build**:
   ```bash
   git clone <repository-url>
   cd MicrosoftUpdateFunctions
   dotnet build
   ```

2. **Start with AppHost** (recommended):
   ```bash
   cd ../MicrosoftUpdateFunctions.AppHost
   dotnet run
   ```

3. **Or start Functions directly**:
   ```bash
   cd src
   func start
   ```

### Configuration

The service layer uses dependency injection with flexible configuration:

```json
{
  "MetadataStorePath": "./store",
  "ContentStorePath": "./content",
  "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\"}"
}
```

## 📡 Available Endpoints

### Sync Operations
- `POST /api/SyncMetadata` - Manual metadata synchronization with filters
- `POST /api/SyncContent` - Content download and management

### Query Operations  
- `GET /api/StoreStatus` - Detailed store status and statistics
- `POST /api/QueryMetadata` - Flexible metadata queries with filtering
- `POST /api/MatchDrivers` - Hardware driver matching
- `POST /api/ExportMetadata` - Export metadata in various formats

### Administrative Operations
- `GET /api/HealthCheck` - Comprehensive system health check
- `POST /api/ReindexStore` - Force metadata store reindexing
- `GET /api/AvailableFilters` - Get available product/classification filters

### SOAP Endpoints (Legacy Support)
- `POST /api/ClientWebService/client.asmx` - Windows Update client sync
- `POST /api/ServerWebService/server.asmx` - WSUS server-to-server sync

### Content Serving
- `GET /api/content/{hash}` - Download update content files

## ⏰ Automated Operations

The system includes comprehensive automation:

- **Every 4 hours**: Critical updates synchronization
- **Daily 2 AM UTC**: Comprehensive metadata sync  
- **Weekly Sunday 3 AM**: Content synchronization
- **Weekly Sunday 1 AM**: Maintenance and cleanup
- **Hourly**: Health monitoring and metrics

## 🧪 Testing

### Unit Tests
```bash
# Test service layer with mocked dependencies
dotnet test --filter "Category!=Integration"
```

### Integration Tests
```bash
# Test against running Functions
dotnet test --filter "Category=Integration"
```

### Service Layer Testing Example
```csharp
[Fact]
public async Task SyncService_ShouldSyncCategories()
{
    // Arrange
    var mockStore = new Mock<IMetadataStore>();
    var syncService = new SyncService(logger, mockStore.Object);
    
    // Act
    await syncService.SyncCategoriesAsync();
    
    // Assert
    mockStore.Verify(x => x.AddCategories(It.IsAny<IEnumerable<Category>>()), Times.Once);
}
```

## 📊 Monitoring

### Health Checks
- System health: `GET /api/HealthCheck`
- Store status: `GET /api/StoreStatus`  
- Service metrics: Built into each endpoint

### Aspire Dashboard
When using AppHost, access enhanced monitoring at `http://localhost:15888`:
- Real-time function execution metrics
- Health check status monitoring
- Resource dependency visualization
- Log aggregation and filtering

## 🔧 Development Features

### Service Layer Pattern
```csharp
// Functions are thin wrappers around services
[Function("SyncMetadata")]
public async Task<HttpResponseData> SyncMetadata(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var request = await ParseRequestAsync<SyncMetadataRequest>(req);
    
    // Business logic handled by service
    var filter = this.syncService.CreateCustomFilter(
        request.ProductFilters, 
        request.ClassificationFilters);
        
    await this.syncService.SyncUpdatesAsync(filter);
    
    return await CreateSuccessResponseAsync(req);
}
```

### Dependency Injection
```csharp
// Clean service registration
services.AddMicrosoftUpdateServices(configuration);
services.AddScoped<ISyncService, SyncService>();
services.AddScoped<IQueryService, QueryService>(); 
services.AddScoped<IHealthService, HealthService>();
```

### Multiple Trigger Support
The same service logic works across:
- **HTTP Triggers**: Interactive operations
- **Timer Triggers**: Scheduled automation
- **ServiceBus Triggers**: Event-driven processing

## 📁 Project Structure

```
MicrosoftUpdateFunctions/
├── src/
│   ├── Functions/           # Azure Functions (thin wrappers)
│   ├── Services/           # Business logic services
│   │   ├── ISyncService.cs
│   │   ├── SyncService.cs
│   │   ├── IQueryService.cs
│   │   ├── QueryService.cs
│   │   ├── IHealthService.cs
│   │   └── HealthService.cs
│   └── Program.cs          # DI configuration
├── tests/
│   ├── Services/           # Service layer unit tests
│   ├── Functions/          # Function integration tests
│   └── Integration/        # End-to-end tests
└── README.md
```

## 🚚 Deployment

### Local Development
```bash
# Using AppHost (recommended)
cd MicrosoftUpdateFunctions.AppHost
dotnet run

# Direct Functions
cd MicrosoftUpdateFunctions/src  
func start
```

### Production Deployment
The service layer architecture supports multiple deployment options:
- Azure Functions consumption/premium plans
- Container deployment with Docker
- Kubernetes deployment with KEDA scaling

## 📚 Documentation

- **Service Layer Guide**: `/src/Services/README.md`
- **Triggers Guide**: `/src/TRIGGERS_GUIDE.md`
- **Testing Guide**: `/tests/TESTING_GUIDE.md`
- **API Documentation**: `/docs/api/`

## 🔄 Migration Notes

If migrating from the previous inline implementation:

1. **No Breaking Changes**: All existing endpoints remain functional
2. **Enhanced Testing**: Service layer enables comprehensive unit testing
3. **Better Maintainability**: Business logic separated from infrastructure concerns
4. **Improved Monitoring**: Enhanced health checks and dependency tracking

## 🤝 Contributing

1. Follow the service layer pattern for new features
2. Add comprehensive unit tests for services
3. Update integration tests for new endpoints  
4. Maintain API compatibility

## 📄 License

MIT License - see LICENSE file for details.