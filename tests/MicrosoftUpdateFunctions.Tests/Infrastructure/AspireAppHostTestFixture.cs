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
    private Process? _funcProcess;
    private Process? _appHostProcess;
    private HttpClient? _httpClient;

    public HttpClient HttpClient => _httpClient ?? throw new InvalidOperationException("Test fixture not initialized");
    public string BaseAddress { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            // Option 1: Try to start with AppHost if available
            await TryStartWithAppHost();
            
            // Option 2: Fallback to direct function startup
            if (_httpClient == null)
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
        _httpClient?.Dispose();
        
        if (_funcProcess != null && !_funcProcess.HasExited)
        {
            _funcProcess.Kill();
            await _funcProcess.WaitForExitAsync();
            _funcProcess.Dispose();
        }
        
        if (_appHostProcess != null && !_appHostProcess.HasExited)
        {
            _appHostProcess.Kill();
            await _appHostProcess.WaitForExitAsync();
            _appHostProcess.Dispose();
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
            var appHostPath = Path.GetFullPath("../../tests/MicrosoftUpdateFunctions.AppHost");
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

                _appHostProcess = Process.Start(startInfo);
                await Task.Delay(15000); // Give AppHost time to start

                // Create HTTP client for the distributed app
                _httpClient = new HttpClient();
                BaseAddress = "http://localhost:7071/api/";
                _httpClient.BaseAddress = new Uri(BaseAddress);
                _httpClient.Timeout = TimeSpan.FromMinutes(2);
            }
        }
        catch
        {
            // AppHost startup failed, will try direct approach
            _appHostProcess?.Dispose();
            _appHostProcess = null;
        }
    }

    private async Task StartAzureFunctionsDirect()
    {
        var funcPath = Path.GetFullPath("../../azure-functions");
        
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

        _funcProcess = Process.Start(startInfo);
        await Task.Delay(10000); // Give Functions time to start

        _httpClient = new HttpClient();
        BaseAddress = "http://localhost:7071/api/";
        _httpClient.BaseAddress = new Uri(BaseAddress);
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    private async Task WaitForServicesReady()
    {
        const int maxRetries = 30;
        const int delayMs = 2000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await _httpClient!.GetAsync("ClientWebService/ClientWebService.asmx");
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