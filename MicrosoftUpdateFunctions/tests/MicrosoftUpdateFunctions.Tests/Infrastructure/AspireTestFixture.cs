using System.Diagnostics;
using System.Net.Http;
using Xunit;

namespace MicrosoftUpdateFunctions.Tests.Infrastructure;

/// <summary>
/// Simplified test fixture that starts Azure Functions using func CLI
/// This provides integration testing without complex Aspire orchestration
/// </summary>
public class AspireTestFixture : IAsyncLifetime
{
    private Process? _funcProcess;

    public HttpClient HttpClient { get; private set; } = null!;
    public string BaseAddress { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            // Start Azure Functions using func CLI
            var currentDir = Directory.GetCurrentDirectory();
            // Navigate from test bin directory to the azure-functions folder
            var funcPath = Path.Combine(currentDir, "../../../../../azure-functions");
            var fullFuncPath = Path.GetFullPath(funcPath);
            
            if (!Directory.Exists(fullFuncPath))
            {
                throw new InvalidOperationException($"Azure Functions directory not found at: {fullFuncPath}");
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "func",
                Arguments = "start --port 7071",
                WorkingDirectory = fullFuncPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _funcProcess = Process.Start(startInfo);
            
            if (_funcProcess == null)
            {
                throw new InvalidOperationException("Failed to start Azure Functions process");
            }

            // Wait for Functions to start up - increased timeout
            await Task.Delay(15000);

            // Create HTTP client for the functions
            HttpClient = new HttpClient();
            BaseAddress = "http://localhost:7071/api/";
            HttpClient.BaseAddress = new Uri(BaseAddress);
            HttpClient.Timeout = TimeSpan.FromMinutes(2);
            
            // Test that the functions are responding with retry logic
            const int maxRetries = 10;
            const int delayMs = 2000;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var healthCheck = await HttpClient.GetAsync("ClientWebService/ClientWebService.asmx");
                    if (healthCheck.IsSuccessStatusCode || healthCheck.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    {
                        return; // Functions are ready
                    }
                }
                catch
                {
                    // Functions not ready yet, continue retrying
                }
                
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs);
                }
            }
            
            throw new InvalidOperationException("Azure Functions are not responding properly after startup");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to initialize Azure Functions test environment. " +
                "Ensure Azure Functions Core Tools are installed and accessible via 'func' command.", ex);
        }
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        
        if (_funcProcess != null && !_funcProcess.HasExited)
        {
            _funcProcess.Kill();
            await _funcProcess.WaitForExitAsync();
            _funcProcess.Dispose();
        }
    }

    public async Task<string> GetFunctionUrl(string functionName)
    {
        // Small delay to ensure function is ready
        await Task.Delay(100);
        return $"{BaseAddress}{functionName}";
    }
}

[CollectionDefinition("AspireIntegration")]
public class AspireIntegrationCollection : ICollectionFixture<AspireTestFixture>
{
}