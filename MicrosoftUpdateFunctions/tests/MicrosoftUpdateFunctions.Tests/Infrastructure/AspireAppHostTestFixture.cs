using System.Diagnostics;
using System.Net.Http;
using Xunit;

namespace MicrosoftUpdateFunctions.Tests.Infrastructure;

/// <summary>
/// Advanced Aspire test fixture that manages Azure Functions with proper lifecycle management
/// This provides comprehensive testing environment for distributed application scenarios
/// </summary>
public class AspireAppHostTestFixture : IAsyncLifetime
{
    private Process? funcProcess;
    private Process? appHostProcess;
    private HttpClient? httpClient;

    public HttpClient HttpClient => this.httpClient ?? throw new InvalidOperationException("Test fixture not initialized");
    public string BaseAddress { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            // Option 1: Try to start with AppHost if available
            await TryStartWithAppHost();
            
            // Option 2: Fallback to direct function startup
            if (this.httpClient == null)
            {
                await StartAzureFunctionsDirect();
            }

            // Health check to ensure functions are responding
            await WaitForServicesReady();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to initialize Aspire AppHost test environment. " +
                "Ensure Azure Functions Core Tools are installed and AppHost builds successfully.", ex);
        }
    }

    public async Task DisposeAsync()
    {
        this.httpClient?.Dispose();
        
        if (this.funcProcess != null && !this.funcProcess.HasExited)
        {
            this.funcProcess.Kill();
            await this.funcProcess.WaitForExitAsync();
            this.funcProcess.Dispose();
        }
        
        if (this.appHostProcess != null && !this.appHostProcess.HasExited)
        {
            this.appHostProcess.Kill();
            await this.appHostProcess.WaitForExitAsync();
            this.appHostProcess.Dispose();
        }
    }

    public async Task<string> GetFunctionUrl(string functionName)
    {
        return $"{BaseAddress}{functionName}";
    }

    private async Task TryStartWithAppHost()
    {
        try
        {
            var appHostPath = Path.GetFullPath("../../../../../tests/MicrosoftUpdateFunctions.AppHost");
            if (Directory.Exists(appHostPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = appHostPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                this.appHostProcess = Process.Start(startInfo);
                await Task.Delay(15000); // Give AppHost time to start

                // Create HTTP client for the distributed app
                this.httpClient = new HttpClient();
                BaseAddress = "http://localhost:7071/api/";
                this.httpClient.BaseAddress = new Uri(BaseAddress);
                this.httpClient.Timeout = TimeSpan.FromMinutes(2);
            }
        }
        catch
        {
            // AppHost startup failed, will try direct approach
            this.appHostProcess?.Dispose();
            this.appHostProcess = null;
        }
    }

    private async Task StartAzureFunctionsDirect()
    {
        var funcPath = Path.GetFullPath("../../../../../azure-functions");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "func",
            Arguments = "start --port 7071",
            WorkingDirectory = funcPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        this.funcProcess = Process.Start(startInfo);
        await Task.Delay(10000); // Give Functions time to start

        this.httpClient = new HttpClient();
        BaseAddress = "http://localhost:7071/api/";
        this.httpClient.BaseAddress = new Uri(BaseAddress);
        this.httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    private async Task WaitForServicesReady()
    {
        const int maxRetries = 30;
        const int delayMs = 2000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await this.httpClient!.GetAsync("ClientWebService/ClientWebService.asmx");
                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed || 
                    response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return; // Service is ready
                }
            }
            catch
            {
                // Service not ready yet
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Services did not become ready within the expected time");
    }
}

[CollectionDefinition("AspireAppHostIntegration")]
public class AspireAppHostIntegrationCollection : ICollectionFixture<AspireAppHostTestFixture>
{
}