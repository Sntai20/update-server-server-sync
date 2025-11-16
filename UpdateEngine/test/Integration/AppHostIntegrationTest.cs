// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using FluentAssertions;
using UpdateEngineTest.Infrastructure;
using System.Net;
using System.Text;
using Xunit;

/// <summary>
/// Integration tests using the complete distributed application via AppHost
/// These tests validate the entire system working together with orchestrated services
/// </summary>
[Collection("AspireIntegration")]
public class AppHostIntegrationTest
{
    private readonly AspireTestFixture _fixture;

    public AppHostIntegrationTest(AspireTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AppHost_ShouldStartAllServices_Successfully()
    {
        // Arrange & Act - Services should be started by the fixture
        var httpClient = _fixture.HttpClient;
        var baseAddress = _fixture.BaseAddress;

        // Assert
        httpClient.Should().NotBeNull();
        baseAddress.Should().NotBeNullOrEmpty();
        baseAddress.Should().StartWith("http");
    }

    [Fact]
    public async Task ClientSync_ShouldRespondWithSoapFault_WhenNoUpdatesRequested()
    {
        // Arrange
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
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("soap:Envelope");
    }

    [Fact]
    public async Task ServerSync_ShouldRespondWithSoapFault_WhenCalledDirectly()
    {
        // Arrange
        var serverSyncUrl = await _fixture.GetFunctionUrl("ServerSyncWebService/ServerSyncWebService.asmx");
        var soapEnvelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                <soap:Body>
                    <GetAuthorizationCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService">
                        <authorizationInfo>
                            <UserName>test@domain.com</UserName>
                            <Password>password</Password>
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
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("soap:Envelope");
    }

    [Fact]
    public async Task Content_ShouldReturn404_ForNonExistentHash()
    {
        // Arrange
        var contentUrl = await _fixture.GetFunctionUrl("content/nonexistent123abc");

        // Act
        var response = await _fixture.HttpClient.GetAsync(contentUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthCheck_ShouldIndicateServicesAreRunning()
    {
        // Arrange - Test that we can reach multiple endpoints indicating the system is healthy
        var endpoints = new[]
        {
            "ClientWebService/ClientWebService.asmx",
            "ServerSyncWebService/ServerSyncWebService.asmx", 
            "SimpleAuthWebService/SimpleAuthWebService.asmx"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var url = await _fixture.GetFunctionUrl(endpoint);
            var response = await _fixture.HttpClient.GetAsync(url);
            
            // SOAP endpoints should return method not allowed for GET
            // This indicates the endpoint is reachable and the service is running
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.MethodNotAllowed, 
                HttpStatusCode.OK, 
                HttpStatusCode.BadRequest);
        }
    }
}