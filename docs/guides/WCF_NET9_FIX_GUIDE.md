# WCF Service Reference .NET 9 Compatibility Issue

## ?? The Problem

The Microsoft Update sync operation fails with the following error:

```
System.NotSupportedException: Method GetAuthConfigAsync is not supported on this proxy, 
this can happen if the method is not marked with OperationContractAttribute or if the 
interface type is not marked with ServiceContractAttribute.
```

### Root Cause

The WCF service references in `src/microsoft-update-webservices/Connected Services/` were generated using **older tooling** that creates proxies incompatible with **.NET 9**.

**Affected Service References:**
- `Microsoft.UpdateServices.WebServices.ServerSync`
- `Microsoft.UpdateServices.WebServices.DssAuthentication`  
- `Microsoft.UpdateServices.WebServices.ClientSync`

The generated proxy classes have `GetAuthConfigAsync`, `GetAuthorizationCookieAsync`, and `GetCookieAsync` methods, but these methods are **not properly attributed** for .NET 9's WCF client implementation.

### Why This Happens

In .NET 9, the `System.ServiceModel` implementation (CoreWCF client) has stricter requirements for service contract attributes. The generated service references from older Visual Studio versions or older `dotnet-svcutil` versions don't meet these requirements.

## ? The Solution

**You MUST regenerate the WCF service references using .NET 9 compatible tooling.**

### Option 1: Using Visual Studio (RECOMMENDED)

This is the easiest and most reliable method:

1. **Open the solution** in Visual Studio 2022 (version 17.8 or later)

2. **Navigate** to Solution Explorer ?  
   `microsoft-update-webservices` project ?  
   `Connected Services`

3. **Update each service reference:**
   - Right-click on `Microsoft.UpdateServices.WebServices.ServerSync`
   - Select **"Update Service Reference"**
   - Review the settings (should auto-detect from existing config)
   - Click **OK**
   
4. **Repeat** for:
   - `Microsoft.UpdateServices.WebServices.DssAuthentication`
   - `Microsoft.UpdateServices.WebServices.ClientSync`

5. **Rebuild** the solution:
   ```powershell
   dotnet build
   ```

6. **Restart** Azure Functions and test

### Option 2: Using Command Line (Advanced)

If you don't have Visual Studio or prefer command-line tools:

1. **Install/Update** dotnet-svcutil:
   ```powershell
   dotnet tool update --global dotnet-svcutil
   ```

2. **Navigate** to repository root:
   ```powershell
   cd D:\repos\update-server-server-sync-fork
   ```

3. **Regenerate ServerSync** reference:
   ```powershell
   dotnet-svcutil https://sws.update.microsoft.com/ServerSyncWebService/ServerSyncWebService.asmx?wsdl `
     --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ServerSync" `
     --namespace "*,Microsoft.UpdateServices.WebServices.ServerSync" `
     --targetFramework net9.0 `
     --outputFile Reference.cs
   ```

4. **Regenerate DssAuthentication** reference:
   ```powershell
   dotnet-svcutil https://sws.update.microsoft.com/DssAuthWebService/DssAuthWebService.asmx?wsdl `
     --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.DssAuthentication" `
     --namespace "*,Microsoft.UpdateServices.WebServices.DssAuthentication" `
     --targetFramework net9.0 `
     --outputFile Reference.cs
   ```

5. **Regenerate ClientSync** reference:
   ```powershell
   dotnet-svcutil https://sws.update.microsoft.com/ClientWebService/client.asmx?wsdl `
     --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ClientSync" `
     --namespace "*,Microsoft.UpdateServices.WebServices.ClientSync" `
   --targetFramework net9.0 `
     --outputFile Reference.cs
   ```

6. **Rebuild** and test:
   ```powershell
dotnet build
   dotnet run --project AppHost
   ```

### ?? Potential Issues with Command Line Method

- May generate slightly different code structure
- Namespace conflicts may occur
- May need manual fixes to compilation errors
- **Visual Studio method is more reliable**

## ?? What Changes After Regeneration

The newly generated `Reference.cs` files will:

? Have proper `[OperationContract]` attributes on async methods  
? Generate Begin/End APM methods if needed  
? Use .NET 9 compatible WCF client patterns  
? Include proper service contract attributes  

## ?? Alternative Workaround (NOT RECOMMENDED)

If you cannot regenerate the service references immediately, there's a temporary workaround:

**Revert to .NET 8** for the Functions project:
- Change `<TargetFramework>net9.0</TargetFramework>` to `net8.0` in project files
- Rebuild

However, this defeats the purpose of upgrading to .NET 9 and should only be used as a last resort.

## ?? Testing After Fix

1. **Rebuild everything:**
   ```powershell
   dotnet clean
   dotnet build
   ```

2. **Restart Azure Functions:**
   ```powershell
 dotnet run --project AppHost
   ```

3. **Test the sync:**
   ```powershell
   $port = <port-from-output>
   $syncRequest = @{ categories = @("Security Updates"); maxUpdates = 5 } | ConvertTo-Json
   Invoke-RestMethod -Uri "http://localhost:$port/api/SyncMetadata" -Method Post -Body $syncRequest -ContentType "application/json"
   ```

4. **Expected result:**
   - No more "Method not supported" errors
   - Authentication succeeds
   - Metadata sync completes
   - Updates are added to storage

## ?? References

- [.NET 9 WCF Client Changes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/runtime#system-servicemodel)
- [dotnet-svcutil Tool Guide](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide)
- [WCF Client Migration Guide](https://learn.microsoft.com/en-us/dotnet/core/porting/upgrade-assistant-wcf)
- [Visual Studio Service Reference](https://learn.microsoft.com/en-us/visualstudio/data-tools/how-to-add-update-or-remove-a-wcf-data-service-reference)

## ?? Why This Issue Exists

The WCF service references were likely generated when the project was on an earlier .NET version (.NET Framework, .NET Core 3.1, or .NET 6/7/8). When upgrading to .NET 9:

1. The project files were updated to `<TargetFramework>net9.0</TargetFramework>`
2. **But** the WCF service references were NOT regenerated
3. .NET 9's WCF implementation is stricter about attributes
4. Old generated proxies fail with .NET 9's runtime checks

This is a **common migration issue** when upgrading WCF client projects to newer .NET versions.

## ? Summary

**Problem:** WCF async methods fail with "not supported" error in .NET 9  
**Cause:** Old service reference generation incompatible with .NET 9  
**Solution:** Regenerate service references using Visual Studio or dotnet-svcutil  
**Time:** ~5-10 minutes  
**Difficulty:** Easy (Visual Studio) / Moderate (Command line)  

After regenerating, the sync operations will work correctly! ??
