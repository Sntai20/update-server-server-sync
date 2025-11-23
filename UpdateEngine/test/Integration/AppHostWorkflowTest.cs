namespace UpdateEngineTest.Integration;

using System.Net.Http;
using System.Text;
using UpdateEngineTest.Infrastructure;
using Xunit;

[Collection("AspireAppHost")]
public class AppHostWorkflowTest
{
    private readonly AspireAppHostTestFixture _fixture;

    public AppHostWorkflowTest(AspireAppHostTestFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task MetadataWorkflow_WithAppHost_WorksEndToEnd()
    {
        // 1. Check store status (HttpClient already knows the base URL)
        var statusResponse = await this._fixture.HttpClient.GetAsync("/api/QueryMetadataStoreStatus");
        Assert.True(statusResponse.IsSuccessStatusCode, 
            $"QueryMetadataStoreStatus failed: {statusResponse.StatusCode}");

        // 2. Fetch configuration
        var configRequest = new HttpRequestMessage(HttpMethod.Post, "/api/FetchConfiguration");
        configRequest.Content = new StringContent(
            "{\"endpoint\":\"default\"}", 
            Encoding.UTF8, 
            "application/json");
        
        var configResponse = await this._fixture.HttpClient.SendAsync(configRequest);
        Assert.True(configResponse.IsSuccessStatusCode,
            $"FetchConfiguration failed: {configResponse.StatusCode}");

        // 3. Test SOAP endpoint
        var soapRequest = this.CreateSimpleSoapRequest();
        var soapResponse = await this._fixture.HttpClient.SendAsync(soapRequest);
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, soapResponse.StatusCode);
    }

    private HttpRequestMessage CreateSimpleSoapRequest()
    {
        var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetConfig xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"" />
  </soap:Body>
</soap:Envelope>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ClientWebService/ClientWebService.asmx");
        request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        request.Headers.Add("SOAPAction", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetConfig");
        
        return request;
    }
}
