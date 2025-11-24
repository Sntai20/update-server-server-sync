# Week 3 Quick Reference Guide

Quick checklist for implementing Week 3 caching integration and enhancements.

---

## ?? Quick Start Checklist

### Prerequisites
- [x] Week 2 Phase 2 & 3 complete
- [x] CacheService implemented
- [x] All Week 2 tests passing (35/35)
- [ ] Docker Desktop installed (for Redis)
- [ ] Review Week 3 plan document

---

## ?? Day 1: Orchestrator Integration

### Morning (3-4 hours)

#### 1. MetadataOrchestrator Caching
```bash
# File: UpdateEngine/src/Core/Orchestrators/MetadataOrchestrator.cs

# Add to constructor:
private readonly CacheService? cacheService;

# Cache GetStatisticsAsync:
- Wrap with GetOrSetAsync("metadata:stats", ...)
- Use GetStatisticsExpiration()

# Cache GetUpdateDetailsAsync:
- Wrap with GetOrSetAsync("metadata:update:{id}", ...)
- Use GetUpdateDetailsExpiration()
```

**Test**: Update `MetadataOrchestratorTests.cs` with caching scenarios

#### 2. ContentOrchestrator Caching
```bash
# File: UpdateEngine/src/Core/Orchestrators/ContentOrchestrator.cs

# Add to constructor:
private readonly CacheService? cacheService;

# Cache GetStatisticsAsync:
- Wrap with GetOrSetAsync("content:stats", ...)

# Cache CheckContentAvailabilityAsync:
- Loop through updates
- Use GetOrSetAsync("content:availability:{id}", ...)
```

**Test**: Update `ContentOrchestratorTests.cs` with caching scenarios

### Afternoon (2-3 hours)

#### 3. SyncOrchestrator Invalidation
```bash
# File: UpdateEngine/src/Core/Orchestrators/SyncOrchestrator.cs

# After successful metadata sync:
await cacheService?.InvalidateAllCachesAsync();

# After content download:
await cacheService?.RemoveAsync("content:stats");
foreach (update in downloadedUpdates)
    await cacheService?.RemoveAsync($"content:availability:{id}");
```

**Test**: Create invalidation tests

#### 4. AppHost Redis Setup
```bash
# File: AppHost/src/Program.cs

# Add before UpdateEngine:
var redis = builder.AddRedis("redis").WithRedisCommander();

# Add to UpdateEngine:
.WithReference(redis, "RedisConnection")
.WaitFor(redis)
```

**Verify**: Start AppHost, check Redis container running

---

## ?? Day 2: Configuration & Testing

### Morning (2-3 hours)

#### 5. Update ConfigurationHelper
```bash
# File: AppHost/src/ConfigurationHelper.cs

# Add cache environment variables:
.WithEnvironment("EnableDistributedCache", ...)
.WithEnvironment("StatisticsCacheMinutes", ...)
# etc.
```

#### 6. Update ServiceCollectionExtensions
```bash
# File: UpdateEngine/src/Core/ServiceCollectionExtensions.cs

# Update Redis registration:
- Use configuration.GetConnectionString("RedisConnection")
- Fall back to cacheConfig.RedisConnectionString
- Add logging for connection status
```

#### 7. Update appsettings.example.json
```bash
# File: Configuration/appsettings.example.json

# Add CacheConfiguration section
# Add ConnectionStrings.RedisConnection
```

### Afternoon (4-5 hours)

#### 8. CacheService Unit Tests
```bash
# Create: UpdateEngine/test/Unit/Services/CacheServiceTests.cs

Tests needed:
- GetOrSetAsync_CacheMiss_CallsFactory
- GetOrSetAsync_CacheHit_ReturnsFromCache
- GetAsync/SetAsync/RemoveAsync
- Invalidation methods (3 tests)
- Exception handling
- Expiration helpers
```

**Target**: 15+ tests, all passing

#### 9. Caching Integration Tests
```bash
# Create: UpdateEngine/test/Integration/Orchestrators/CachingIntegrationTests.cs

Tests needed:
- Metadata statistics caching
- Update details caching
- Content statistics caching
- Content availability caching
- Cache invalidation after sync
- Disabled cache scenario
```

**Target**: 7+ tests, all passing

---

## ?? Day 3: Health Checks & Documentation

### Morning (3-4 hours)

#### 10. Redis Health Check
```bash
# Create: UpdateEngine/src/Core/HealthChecks/RedisHealthCheck.cs

Implementation:
- Test write/read/delete operation
- Return Healthy/Degraded/Unhealthy
- Register in ServiceCollectionExtensions
```

**Test**: Verify health endpoint shows Redis status

#### 11. AppHost Integration Test
```bash
# Create: UpdateEngine/test/Integration/AppHostCachingTest.cs

Tests:
- AppHost starts with Redis
- Functions connect to Redis
- Cache operations work end-to-end
```

**Target**: 4+ tests, all passing

### Afternoon (3-4 hours)

#### 12. Create CACHING_GUIDE.md
```bash
Sections:
1. Overview (what, why, how)
2. Configuration
3. Cache patterns
4. Best practices
5. Development setup
6. Production deployment
```

**Target**: Comprehensive user guide

#### 13. Create CACHING_ARCHITECTURE.md
```bash
Sections:
1. Architecture overview with diagrams
2. Implementation details
3. Performance impact
4. Monitoring
```

**Target**: Technical reference document

#### 14. Update Main Documentation
```bash
Files to update:
- README.md (add caching feature)
- .github/copilot-instructions.md (caching patterns)
- Configuration/README.md (if exists)
```

#### 15. Create WEEK3_COMPLETION_SUMMARY.md
```bash
Sections:
- Executive summary
- Features implemented
- Tests created and results
- Configuration changes
- Next steps
```

---

## ?? Testing Checklist

### Unit Tests
```powershell
# Test cache service
dotnet test --filter "FullyQualifiedName~CacheServiceTests"

# Test orchestrators with caching
dotnet test --filter "FullyQualifiedName~OrchestratorTests"
```

### Integration Tests
```powershell
# Start Redis locally
docker run -d -p 6379:6379 redis:latest

# Run caching integration tests
dotnet test --filter "FullyQualifiedName~CachingIntegrationTests"

# Run orchestrator integration tests
dotnet test --filter "FullyQualifiedName~Orchestrators"
```

### AppHost Tests
```powershell
# Full AppHost test suite
dotnet test --filter "Collection=AspireAppHost"
```

### All Tests
```powershell
# Run everything
dotnet test UpdateEngine\test\UpdateEngineTest.csproj
```

**Target**: 60+ total tests (35 existing + 25 new), 100% pass rate

---

## ?? Verification Checklist

### Functional Verification
- [ ] MetadataOrchestrator caches statistics
- [ ] MetadataOrchestrator caches update details
- [ ] ContentOrchestrator caches statistics
- [ ] ContentOrchestrator caches availability
- [ ] SyncOrchestrator invalidates caches after sync
- [ ] Cache disabled mode works (fallback)
- [ ] Redis container starts in AppHost
- [ ] Health check reports Redis status

### Code Quality
- [ ] No build errors or warnings
- [ ] All tests passing (100%)
- [ ] Code follows existing patterns
- [ ] Error handling for cache failures
- [ ] Logging for cache operations
- [ ] Backward compatible (cache optional)

### Configuration
- [ ] CacheConfiguration in appsettings.example.json
- [ ] ConfigurationHelper passes cache config
- [ ] Environment variables mapped correctly
- [ ] Redis connection string configured

### Documentation
- [ ] CACHING_GUIDE.md created
- [ ] CACHING_ARCHITECTURE.md created
- [ ] README.md updated
- [ ] copilot-instructions.md updated
- [ ] WEEK3_COMPLETION_SUMMARY.md created
- [ ] All code comments clear and helpful

---

## ?? Commands Reference

### Start Redis Locally
```powershell
# Docker
docker run -d -p 6379:6379 --name redis redis:latest

# Stop Redis
docker stop redis
docker rm redis
```

### Run AppHost
```powershell
cd AppHost/src
dotnet run
```

### Run Tests
```powershell
# All tests
dotnet test

# Specific category
dotnet test --filter "FullyQualifiedName~Caching"

# With verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Build Solution
```powershell
# Full rebuild
dotnet clean
dotnet build

# Specific project
dotnet build UpdateEngine/src/UpdateEngine.csproj
```

### Check Health Endpoints
```powershell
# AppHost health
curl http://localhost:7071/health

# Detailed health
curl http://localhost:7071/health/ready
```

---

## ?? Success Metrics

### Coverage Targets
- Unit tests: 25+ new tests
- Integration tests: 10+ new tests
- Total tests: 60+ (35 existing + 25 new)
- Pass rate: 100%

### Performance Targets
- Statistics queries: <10ms with cache (vs ~100ms without)
- Update details: <5ms with cache (vs ~50ms without)
- Cache hit rate: >70% for common queries

### Code Quality Targets
- Build: 0 errors, 0 warnings
- Test coverage: >80% for CacheService
- Documentation: Complete and clear

---

## ?? Common Issues

### Issue: Redis connection fails
**Solution**: Check Docker running, port 6379 available, connection string correct

### Issue: Tests fail with cache enabled
**Solution**: Ensure test isolation, clean cache between tests

### Issue: Cache not invalidating
**Solution**: Check InvalidateOnSync flag, verify logging

### Issue: Serialization errors
**Solution**: Verify JSON serialization settings, check object types

---

## ?? Key Concepts

### Cache-Aside Pattern
1. Check cache first
2. If miss, fetch from source
3. Store in cache
4. Return value

### Invalidation Strategy
- **On Sync**: Clear all caches (InvalidateOnSync flag)
- **On Write**: Clear affected caches only
- **On Schedule**: TTL-based expiration

### TTL Guidelines
- Statistics: 5 minutes (frequently updated)
- Update details: 60 minutes (rarely changes)
- Content availability: 15 minutes (moderate)

---

## ?? Resources

### Documentation
- `/docs/guides/WEEK3_CACHING_INTEGRATION_PLAN.md` - Full plan
- `/docs/guides/WEEK2_PHASE2_AND_3_PROGRESS.md` - Week 2 status
- `.github/copilot-instructions.md` - Project guidelines

### Code References
- `CacheService.cs` - Cache implementation
- `ServiceCollectionExtensions.cs` - DI registration
- `*Orchestrator.cs` - Integration points

### External Resources
- [Redis Documentation](https://redis.io/docs/)
- [.NET Aspire Redis](https://learn.microsoft.com/en-us/dotnet/aspire/caching/stackexchange-redis-component)
- [Cache-Aside Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)

---

**Created**: 2025-01-17  
**For**: Week 3 Implementation  
**Status**: Ready to execute
