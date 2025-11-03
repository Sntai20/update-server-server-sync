// Create a simple test file to debug the issue
// filepath: d:\repos\update-server-server-sync-fork\UpdateEngine\test\Integration\DiagnosticTest.cs

using System.Net;
using UpdateEngineTest.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace UpdateEngineTest.Integration;

[Collection("AspireAppHost")]
public class DiagnosticTest
{
    private readonly AspireAppHostTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DiagnosticTest(AspireAppHostTestFixture fixture, ITestOutputHelper output)
    {
        this._fixture = fixture;
        this._output = output;
    }

    [Fact]
    public async Task Debug_CheckBaseConnection()
    {
        this._output.WriteLine($"HttpClient Base Address: {this._fixture.HttpClient.BaseAddress}");
        
        try
        {
            // Try to get a simple response
            var response = await this._fixture.HttpClient.GetAsync("/");
            this._output.WriteLine($"Root response: {response.StatusCode}");
            
            var content = await response.Content.ReadAsStringAsync();
            this._output.WriteLine($"Root content: {content}");
        }
        catch (Exception ex)
        {
            this._output.WriteLine($"Root request failed: {ex.Message}");
        }

        try
        {
            // Try the health endpoint
            var healthResponse = await this._fixture.HttpClient.GetAsync("/api/UniversalHealth");
            this._output.WriteLine($"Health response: {healthResponse.StatusCode}");
            
            var healthContent = await healthResponse.Content.ReadAsStringAsync();
            this._output.WriteLine($"Health content: {healthContent}");
        }
        catch (Exception ex)
        {
            this._output.WriteLine($"Health request failed: {ex.Message}");
        }
    }
}