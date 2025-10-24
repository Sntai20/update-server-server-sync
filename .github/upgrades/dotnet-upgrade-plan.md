# .NET 9.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 9.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 9.0 upgrade.
3. Upgrade src\microsoft-update-webservices\microsoft-update-webservices.csproj
4. Upgrade src\microsoft-update-partition\microsoft-update-partition.csproj
5. Upgrade src\microsoft-update-upstream-package-source\microsoft-update-upstream-source.csproj
6. Upgrade src\microsoft-update-endpoints\microsoft-update-endpoints.csproj
7. Upgrade MicrosoftUpdateFunctions\src\MicrosoftUpdateFunctions.csproj
8. Upgrade MicrosoftUpdateFunctions.AppHost\MicrosoftUpdateFunctions.AppHost.csproj
9. Upgrade MicrosoftUpdateFunctions\tests\MicrosoftUpdateFunctions.Tests\MicrosoftUpdateFunctions.Tests.csproj
10. Run unit tests to validate upgrade in the projects listed below:
   - MicrosoftUpdateFunctions\tests\MicrosoftUpdateFunctions.Tests\MicrosoftUpdateFunctions.Tests.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name  | Current Version | New Version | Description    |
|:--------------------------------------------------------|:---------------:|:-----------:|:-------------------------------------------------------------|
| Aspire.Hosting              | 9.5.2  |      | Replace with Aspire.Hosting.AppHost 9.5.2         |
| Aspire.Hosting.AppHost |        | 9.5.2       | Replacement for Aspire.Hosting                |
| Aspire.Hosting.Azure.Functions         | 9.5.1-preview.1.25502.11 | 9.5.2-preview.1.25522.3 | Recommended for .NET 9.0          |
| Aspire.Hosting.Azure.Storage      | 9.5.1           | 9.5.2       | Recommended for .NET 9.0                |
| CoreWCF.ConfigurationManager     | | 1.8.0       | Replacement for System.ServiceModel packages        |
| CoreWCF.Http             |      | 1.8.0       | Replacement for System.ServiceModel packages |
| CoreWCF.NetTcp  |         | 1.8.0   | Replacement for System.ServiceModel packages            |
| CoreWCF.Primitives     |               | 1.8.0       | Replacement for System.ServiceModel packages |
| CoreWCF.WebHttp        |   | 1.8.0       | Replacement for System.ServiceModel packages          |
| Microsoft.AspNetCore.Mvc.Testing   | 8.0.0   | 9.0.10      | Recommended for .NET 9.0         |
| Microsoft.Azure.Functions.Worker.Extensions.EventHubs   |        | 5.6.0       | Additional package recommended with ServiceBus extensions  |
| Microsoft.Azure.Functions.Worker.Extensions.ServiceBus  | 5.24.0| 5.24.0      | Recommended for .NET 9.0     |
| Newtonsoft.Json       | 13.0.1       | 13.0.4      | Recommended for .NET 9.0          |
| System.ComponentModel.Annotations                 | 5.0.0    |       | Package functionality included with framework reference  |
| System.ServiceModel.Duplex        | 4.10.0          |   | Replace with CoreWCF packages        |
| System.ServiceModel.Http             | 4.10.0; 6.0.0   | | Replace with CoreWCF packages           |
| System.ServiceModel.NetTcp     | 4.10.0  |       | Replace with CoreWCF packages            |
| System.ServiceModel.Primitives        | 6.0.0     |             | Replace with CoreWCF packages        |
| System.ServiceModel.Security      | 4.10.0          |             | Replace with CoreWCF packages          |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### src\microsoft-update-webservices\microsoft-update-webservices.csproj modifications

Project properties changes:
  - Target framework should be changed from `net6.0` to `net9.0`

NuGet packages changes:
  - System.ServiceModel.Duplex should be removed (*Replace with CoreWCF packages*)
  - System.ServiceModel.Http should be removed (*Replace with CoreWCF packages*)
  - System.ServiceModel.NetTcp should be removed (*Replace with CoreWCF packages*)
  - System.ServiceModel.Security should be removed (*Replace with CoreWCF packages*)
  - CoreWCF.Primitives version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.ConfigurationManager version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.Http version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.WebHttp version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.NetTcp version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)

Feature upgrades:
  - Migrate .NET Framework WCF services to CoreWCF

#### src\microsoft-update-partition\microsoft-update-partition.csproj modifications

Project properties changes:
  - Target framework should be changed from `net6.0` to `net9.0`

NuGet packages changes:
  - Newtonsoft.Json should be updated from `13.0.1` to `13.0.4` (*Recommended for .NET 9.0*)

#### src\microsoft-update-upstream-package-source\microsoft-update-upstream-source.csproj modifications

Project properties changes:
  - Target framework should be changed from `net6.0` to `net9.0`

NuGet packages changes:
  - Newtonsoft.Json should be updated from `13.0.1` to `13.0.4` (*Recommended for .NET 9.0*)

#### src\microsoft-update-endpoints\microsoft-update-endpoints.csproj modifications

Project properties changes:
  - Target framework should be changed from `net6.0` to `net9.0`

#### MicrosoftUpdateFunctions\src\MicrosoftUpdateFunctions.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net9.0`

NuGet packages changes:
  - Microsoft.Azure.Functions.Worker.Extensions.ServiceBus version `5.24.0` should be retained (*Recommended for .NET 9.0*)
  - Microsoft.Azure.Functions.Worker.Extensions.EventHubs version `5.6.0` should be added (*Additional package recommended with ServiceBus extensions*)
  - System.ComponentModel.Annotations should be removed (*Package functionality included with framework reference*)

#### MicrosoftUpdateFunctions.AppHost\MicrosoftUpdateFunctions.AppHost.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net9.0`

NuGet packages changes:
  - Aspire.Hosting should be removed (*Replace with Aspire.Hosting.AppHost 9.5.2*)
  - Aspire.Hosting.AppHost version `9.5.2` should be added (*Replacement for Aspire.Hosting*)
  - Aspire.Hosting.Azure.Functions should be updated from `9.5.1-preview.1.25502.11` to `9.5.2-preview.1.25522.3` (*Recommended for .NET 9.0*)
  - Aspire.Hosting.Azure.Storage should be updated from `9.5.1` to `9.5.2` (*Recommended for .NET 9.0*)

#### MicrosoftUpdateFunctions\tests\MicrosoftUpdateFunctions.Tests\MicrosoftUpdateFunctions.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net9.0`

NuGet packages changes:
  - Microsoft.AspNetCore.Mvc.Testing should be updated from `8.0.0` to `9.0.10` (*Recommended for .NET 9.0*)
  - System.ServiceModel.Http should be removed (*Replace with CoreWCF packages*)
  - System.ServiceModel.Primitives should be removed (*Replace with CoreWCF packages*)
  - CoreWCF.Primitives version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.ConfigurationManager version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.Http version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.WebHttp version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
  - CoreWCF.NetTcp version `1.8.0` should be added (*Replacement for System.ServiceModel packages*)
