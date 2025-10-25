using FluentAssertions;
using UpdateEngine.Tests.Infrastructure;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using Xunit;

namespace UpdateEngine.Tests.Integration;

/// <summary>
/// Comprehensive system tests that validate the complete Microsoft Update server implementation
/// including both serving capabilities (SOAP endpoints) and management capabilities (metadata sync)
/// </summary>
[Collection("AspireIntegration")]
public class CompleteSystemIntegrationTests
{
    private readonly AspireTestFixture _fixture;

    public CompleteSystemIntegrationTests(AspireTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteSystem_ShouldProvideAllRequiredEndpoints()
    {
        // Arrange - Define all expected endpoints
        var soapEndpoints = new[]
        {
            "ClientWebService/ClientWebService.asmx",
            "ServerSyncWebService/ServerSyncWebService.asmx",
            "SimpleAuthWebService/SimpleAuthWebService.asmx",
            "ReportingWebService/ReportingWebService.asmx"
        };

        var metadataEndpoints = new[]
        {
            "metadata/fetch-configuration",
            "metadata/fetch-categories",
            "metadata/fetch-updates", 
            "metadata/reindex-store",
            "metadata/store-status"
        };

        var contentEndpoints = new[]
        {
            "content/sha1/test123",
            "content/sha256/test456"
        };

        // Act & Assert - Test SOAP endpoints
        foreach (var endpoint in soapEndpoints)
        {
            var url = await _fixture.GetFunctionUrl(endpoint);
            var response = await _fixture.HttpClient.GetAsync(url);
            
            // SOAP endpoints should respond (even if method not allowed for GET)
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.MethodNotAllowed,
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest);
        }

        // Test metadata endpoints (POST)
        foreach (var endpoint in metadataEndpoints.Where(e => !e.Contains("store-status")))
        {
            var url = await _fixture.GetFunctionUrl(endpoint);
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _fixture.HttpClient.PostAsync(url, content);
            
            // Should be reachable and handle requests
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError);
        }

        // Test store status endpoint (GET)
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var statusResponse = await _fixture.HttpClient.GetAsync(statusUrl);
        statusResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);

        // Test content endpoints
        foreach (var endpoint in contentEndpoints)
        {
            var url = await _fixture.GetFunctionUrl(endpoint);
            var response = await _fixture.HttpClient.GetAsync(url);
            
            // Content endpoints should return 404 for non-existent content
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task WindowsUpdateClient_ShouldReceiveValidSoapResponse()
    {
        // Arrange - Simulate Windows Update client GetConfig request
        var clientSyncUrl = await _fixture.GetFunctionUrl("ClientWebService/ClientWebService.asmx");
        var soapEnvelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
                           xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
                           xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                <soap:Body>
                    <GetConfig xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                        <protocolVersion>3.0.0.0</protocolVersion>
                        <lastChange>2023-01-01T00:00:00Z</lastChange>
                    </GetConfig>
                </soap:Body>
            </soap:Envelope>
            """;

        // Act
        var response = await _fixture.HttpClient.PostAsync(clientSyncUrl, 
            new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("soap:Envelope");
        content.Should().Contain("GetConfigResponse");
    }

    [Fact]
    public async Task WSUSServer_ShouldReceiveValidServerSyncResponse()
    {
        // Arrange - Simulate WSUS server authorization request
        var serverSyncUrl = await _fixture.GetFunctionUrl("ServerSyncWebService/ServerSyncWebService.asmx");
        var soapEnvelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                <soap:Body>
                    <GetAuthorizationCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService">
                        <authorizationInfo>
                            <UserName>admin@domain.com</UserName>
                            <Password>password123</Password>
                        </authorizationInfo>
                    </GetAuthorizationCookie>
                </soap:Body>
            </soap:Envelope>
            """;

        // Act
        var response = await _fixture.HttpClient.PostAsync(serverSyncUrl, 
            new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("soap:Envelope");
    }

    [Fact]
    public async Task Administrator_ShouldBeAbleToManageMetadataSync()
    {
        // Arrange - Simulate administrator workflow for managing updates
        
        // Step 1: Check current store status
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var statusResponse = await _fixture.HttpClient.GetAsync(statusUrl);
        statusResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // Step 2: Fetch configuration from upstream
        var configUrl = await _fixture.GetFunctionUrl("metadata/fetch-configuration");
        var configRequest = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com"
        };
        var configContent = new StringContent(
            JsonConvert.SerializeObject(configRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var configResponse = await _fixture.HttpClient.PostAsync(configUrl, configContent);
        configResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var configData = await configResponse.Content.ReadAsStringAsync();
        configData.Should().NotBeNullOrEmpty();

        // Step 3: Fetch categories with filtering
        var categoriesUrl = await _fixture.GetFunctionUrl("metadata/fetch-categories");
        var categoriesRequest = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            MaxCategories = 10
        };
        var categoriesContent = new StringContent(
            JsonConvert.SerializeObject(categoriesRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var categoriesResponse = await _fixture.HttpClient.PostAsync(categoriesUrl, categoriesContent);
        categoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Sync specific updates
        var updatesUrl = await _fixture.GetFunctionUrl("metadata/fetch-updates");
        var updatesRequest = new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com",
            ProductsFilter = new[] { "Windows 10", "Windows 11" },
            ClassificationsFilter = new[] { "Security Updates", "Critical Updates" },
            MaxUpdates = 5,
            SkipSuperseded = true
        };
        var updatesContent = new StringContent(
            JsonConvert.SerializeObject(updatesRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var updatesResponse = await _fixture.HttpClient.PostAsync(updatesUrl, updatesContent);
        updatesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Reindex store for optimal performance
        var reindexUrl = await _fixture.GetFunctionUrl("metadata/reindex-store");
        var reindexRequest = new
        {
            StorePath = "./store"
        };
        var reindexContent = new StringContent(
            JsonConvert.SerializeObject(reindexRequest), 
            Encoding.UTF8, 
            "application/json");
        
        var reindexResponse = await _fixture.HttpClient.PostAsync(reindexUrl, reindexContent);
        reindexResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.NotFound); // Acceptable if store doesn't exist in test
    }

    [Fact]
    public async Task LoadBalancer_ShouldDistributeRequestsAcrossEndpoints()
    {
        // Arrange - Test multiple concurrent requests to different endpoints
        var tasks = new List<Task<HttpResponseMessage>>();

        // SOAP endpoint requests
        var clientSoapRequest = CreateSoapRequest("ClientWebService/ClientWebService.asmx", """
            <GetConfig xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                <protocolVersion>3.0.0.0</protocolVersion>
                <lastChange>2023-01-01T00:00:00Z</lastChange>
            </GetConfig>
            """);
        tasks.Add(clientSoapRequest);

        var serverSoapRequest = CreateSoapRequest("ServerSyncWebService/ServerSyncWebService.asmx", """
            <GetAuthorizationCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService">
                <authorizationInfo>
                    <UserName>test@domain.com</UserName>
                    <Password>password</Password>
                </authorizationInfo>
            </GetAuthorizationCookie>
            """);
        tasks.Add(serverSoapRequest);

        // Metadata sync requests
        var configTask = CreateMetadataRequest("metadata/fetch-configuration", new
        {
            UpstreamEndpoint = "https://sws.update.microsoft.com"
        });
        tasks.Add(configTask);

        var statusTask = _fixture.HttpClient.GetAsync(await _fixture.GetFunctionUrl("metadata/store-status"));
        tasks.Add(statusTask);

        // Content requests
        var contentTask = _fixture.HttpClient.GetAsync(await _fixture.GetFunctionUrl("content/sha256/nonexistent"));
        tasks.Add(contentTask);

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(5);
        
        // All endpoints should respond appropriately
        responses[0].StatusCode.Should().Be(HttpStatusCode.OK); // Client SOAP
        responses[1].StatusCode.Should().Be(HttpStatusCode.OK); // Server SOAP
        responses[2].StatusCode.Should().Be(HttpStatusCode.OK); // Config metadata
        responses[3].StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound); // Status
        responses[4].StatusCode.Should().Be(HttpStatusCode.NotFound); // Content

        // Dispose responses
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task SystemResilience_ShouldHandleInvalidRequests()
    {
        // Arrange & Act - Test various invalid request scenarios
        var tests = new[]
        {
            // Invalid SOAP requests
            TestInvalidSoap("ClientWebService/ClientWebService.asmx", "invalid xml"),
            TestInvalidSoap("ServerSyncWebService/ServerSyncWebService.asmx", "<invalid>"),
            
            // Invalid metadata requests
            TestInvalidMetadata("metadata/fetch-configuration", "invalid json"),
            TestInvalidMetadata("metadata/fetch-categories", "{ invalid }"),
            
            // Invalid content requests
            TestInvalidContent("content/invalidhash"),
            TestInvalidContent("content/sha256/"),
            
            // Wrong HTTP methods
            TestWrongMethod("ClientWebService/ClientWebService.asmx", HttpMethod.Put),
            TestWrongMethod("metadata/fetch-configuration", HttpMethod.Get)
        };

        var responses = await Task.WhenAll(tests);

        // Assert - All should handle errors gracefully
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.MethodNotAllowed,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError);
            response.Dispose();
        }
    }

    [Fact]
    public async Task PerformanceBaseline_ShouldMeetResponseTimeRequirements()
    {
        // Arrange - Test response times for key endpoints
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Test lightweight operations
        var statusUrl = await _fixture.GetFunctionUrl("metadata/store-status");
        var statusResponse = await _fixture.HttpClient.GetAsync(statusUrl);
        var statusTime = stopwatch.ElapsedMilliseconds;

        stopwatch.Restart();
        var contentUrl = await _fixture.GetFunctionUrl("content/sha256/test123");
        var contentResponse = await _fixture.HttpClient.GetAsync(contentUrl);
        var contentTime = stopwatch.ElapsedMilliseconds;

        stopwatch.Stop();

        // Assert - Response times should be reasonable for Azure Functions
        statusTime.Should().BeLessThan(5000); // 5 seconds max for status check
        contentTime.Should().BeLessThan(3000); // 3 seconds max for content lookup

        statusResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        contentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        statusResponse.Dispose();
        contentResponse.Dispose();
    }

    // Helper methods
    private async Task<HttpResponseMessage> CreateSoapRequest(string endpoint, string soapBody)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        var soapEnvelope = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                <soap:Body>
                    {soapBody}
                </soap:Body>
            </soap:Envelope>
            """;
        
        return await _fixture.HttpClient.PostAsync(url, 
            new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"));
    }

    private async Task<HttpResponseMessage> CreateMetadataRequest(string endpoint, object requestData)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        var json = JsonConvert.SerializeObject(requestData);
        return await _fixture.HttpClient.PostAsync(url, 
            new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private async Task<HttpResponseMessage> TestInvalidSoap(string endpoint, string invalidXml)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        return await _fixture.HttpClient.PostAsync(url, 
            new StringContent(invalidXml, Encoding.UTF8, "text/xml"));
    }

    private async Task<HttpResponseMessage> TestInvalidMetadata(string endpoint, string invalidJson)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        return await _fixture.HttpClient.PostAsync(url, 
            new StringContent(invalidJson, Encoding.UTF8, "application/json"));
    }

    private async Task<HttpResponseMessage> TestInvalidContent(string endpoint)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        return await _fixture.HttpClient.GetAsync(url);
    }

    private async Task<HttpResponseMessage> TestWrongMethod(string endpoint, HttpMethod method)
    {
        var url = await _fixture.GetFunctionUrl(endpoint);
        var request = new HttpRequestMessage(method, url);
        return await _fixture.HttpClient.SendAsync(request);
    }
}