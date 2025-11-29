# Azurite Storage Configuration Options

This guide explains the different storage persistence options for Azurite in the Aspire AppHost.

## Quick Comparison

| Option | Persistence | Inspection | Performance | Best For |
|--------|-------------|------------|-------------|----------|
| **No Volume** | ? Lost on restart | ? No | ? Fast | Quick tests |
| **Named Volume** | ? Persists | ? Docker only | ? Fast | General dev |
| **Bind Mount** | ? Persists | ? Direct file access | ? Fast | Debugging |
| **Host Azurite** | ? Persists | ? Direct file access | ?? Fastest | Performance |

## Configuration Options

### Option 1: In-Memory (Default - Not Recommended)

```csharp
var storage = builder.AddAzureStorage("Storage").RunAsEmulator();
```

**Pros:**
- Simplest configuration
- Fast startup

**Cons:**
- ? **Data lost on container restart**
- ? Have to re-sync all updates every time
- ? Slow development iterations

### Option 2: Named Volume (Good)

```csharp
var storage = builder.AddAzureStorage("Storage").RunAsEmulator(emulator =>
{
    emulator.WithDataVolume("aspire-azurite-data");
});
```

**Pros:**
- ? Data persists across restarts
- ? Isolated from host filesystem
- ? Easy cleanup: `docker volume rm aspire-azurite-data`

**Cons:**
- Cannot easily inspect blob files
- Requires Docker volume commands to manage

**Manage the volume:**
```powershell
# List volumes
docker volume ls

# Inspect volume
docker volume inspect aspire-azurite-data

# Remove volume (clean slate)
docker volume rm aspire-azurite-data
```

### Option 3: Bind Mount (Recommended ?)

```csharp
var storage = builder.AddAzureStorage("Storage").RunAsEmulator(emulator =>
{
    emulator.WithDataBindMount("out/azurite-data");
});
```

**Pros:**
- ? Data persists across restarts
- ? **Direct file access** - see blobs in `out/azurite-data/`
- ? Easy inspection and debugging
- ? Simple cleanup: delete the folder
- ? Can backup/restore by copying directory
- ? Organized with other build outputs in `out/` directory

**Cons:**
- Directory appears in workspace (already covered by `out/` in `.gitignore`)

**Inspect the data:**
```powershell
# See all blob files
ls out/azurite-data/__blobstorage__/

# View container structure
ls out/azurite-data/__blobstorage__/data/

# Clean up
Remove-Item -Recurse -Force out/azurite-data
```

### Option 4: Host Azurite Instance (Advanced)

Install Azurite globally and run outside Docker:

```powershell
# Install (one-time)
npm install -g azurite

# Run Azurite on host
azurite --location ./azurite-data --debug ./azurite-debug.log

# Keep running in separate terminal
```

AppHost configuration:
```csharp
// Use connection string to point to host instance
var storage = builder.Environment.EnvironmentName == Environments.Development
    ? builder.AddConnectionString("Storage", "UseDevelopmentStorage=true")
    : builder.AddAzureStorage("Storage");
```

**Pros:**
- ?? **Best performance** (no container overhead)
- ? Data persists
- ? Full control over Azurite process
- ? Better debugging logs

**Cons:**
- Requires Node.js/npm
- Need to manage separate process
- More complex setup

## Current Configuration

The AppHost is currently configured to use **Option 3 (Bind Mount)** at `out/azurite-data/`:

```csharp
var storage = builder.AddAzureStorage("Storage").RunAsEmulator(emulator =>
{
    emulator.WithDataBindMount("out/azurite-data");
});
```

## Data Directory Structure

When using bind mount, the directory structure looks like:

```
out/azurite-data/
??? __blobstorage__/
?   ??? data/                    # "data" container
?   ?   ??? content/            # Content blobs
?   ?   ?   ??? {guid}_{hash}   # Update content files
?   ?   ??? {guid}              # Metadata blobs
?   ??? devstoreaccount1/       # Default account
??? __queuestorage__/           # Queue storage (if used)
??? __azurite_db_*.json         # Azurite metadata
```

## Troubleshooting

### Data Not Persisting

**Symptom:** Have to re-sync after every restart

**Solution:** Check that you're using bind mount or named volume:
```csharp
emulator.WithDataBindMount("./azurite-data");  // ? Good
// or
emulator.WithDataVolume("aspire-azurite-data"); // ? Good
```

### Cannot See Blob Files

**Symptom:** Want to inspect blob data but can't find it

**Solution:** Use bind mount instead of named volume:
```csharp
emulator.WithDataBindMount("out/azurite-data");  // ? Creates visible directory
```

### Permission Errors

**Symptom:** Docker can't write to bind mount directory

**Solution:** Ensure directory exists and has correct permissions:
```powershell
# Windows: Create directory (Docker will handle permissions)
New-Item -ItemType Directory -Force out/azurite-data

# Linux: Ensure permissions
mkdir -p out/azurite-data
chmod 777 out/azurite-data  # Or more restrictive as needed
```

### Corrupted Azurite Data

**Symptom:** Azurite won't start or behaves strangely

**Solution:** Delete the data directory and restart:
```powershell
# Stop AppHost
# Delete Azurite data
Remove-Item -Recurse -Force out/azurite-data
# Restart AppHost - will create fresh Azurite
```

## Best Practices

1. **Already in `.gitignore`**: The `out/` directory pattern covers this
   ```gitignore
   out/
   ```

2. **Use Bind Mount for Development**: Makes debugging easier
   ```csharp
   emulator.WithDataBindMount("out/azurite-data");
   ```

3. **Document Data Location**: Add comments in `Program.cs`
   ```csharp
   // Blob files will be visible in out/azurite-data directory
   ```

4. **Periodic Cleanup**: Delete old data when starting fresh
   ```powershell
   Remove-Item -Recurse -Force out/azurite-data
   ```

5. **Backup Important Data**: Copy directory before major changes
   ```powershell
   Copy-Item -Recurse out/azurite-data out/azurite-data-backup
   ```

## Migration from In-Memory to Bind Mount

If you were previously using in-memory storage and want to migrate:

1. **Stop AppHost** (Ctrl+C)

2. **Update `Program.cs`**:
   ```csharp
   var storage = builder.AddAzureStorage("Storage").RunAsEmulator(emulator =>
   {
       emulator.WithDataBindMount("out/azurite-data");
   });
   ```

3. **`.gitignore` already covers it** via the `out/` pattern

4. **Restart AppHost**:
   ```powershell
   cd UpdateEngine.AppHost/src
   dotnet run
   ```

5. **Data persists** from now on! ??

## See Also

- [Aspire Azure Storage Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/storage/azure-storage-emulator)
- [Azurite GitHub](https://github.com/Azure/Azurite)
- [Docker Volumes Documentation](https://docs.docker.com/storage/volumes/)
