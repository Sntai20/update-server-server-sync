// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Endpoints;

using FluentAssertions;
using UpdateEngineTest.Infrastructure;
using System.Net;
using Xunit;
using Xunit.Abstractions;

[Collection("MicrosoftUpdate")]
public class ClientSyncEndpointTest
{
    private readonly MicrosoftUpdateTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ClientSyncEndpointTest(MicrosoftUpdateTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ClientWebService_ShouldRespondToGetConfigRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateGetConfigRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("GetConfig", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ClientWebService/client.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var soapResponse = await SoapTestHelper.ParseSoapResponse(response);
        soapResponse.Should().NotBeNull();
        
        // Check for SOAP envelope structure
        var envelope = soapResponse.SelectSingleNode("//soap:Envelope", CreateNamespaceManager(soapResponse));
        envelope.Should().NotBeNull();

        _output.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ClientWebService_ShouldRespondToSyncUpdatesRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateSyncUpdatesRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("SyncUpdates", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ClientWebService/client.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("SyncUpdatesResponse")
            .And.Contain("soap:Envelope");

        _output.WriteLine($"SyncUpdates Response: {responseContent}");
    }

    [Fact]
    public async Task ClientWebService_ShouldRejectInvalidSoapRequests()
    {
        // Arrange
        var invalidSoapRequest = new StringContent("invalid xml", System.Text.Encoding.UTF8, "text/xml");

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ClientWebService/client.asmx"), 
            invalidSoapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("soap:Fault");

        _output.WriteLine($"Error Response: {responseContent}");
    }

    [Fact]
    public async Task SimpleAuthWebService_ShouldRespondToAuthRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateGetAuthorizationCookieRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("GetAuthorizationCookie", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("SimpleAuthWebService/SimpleAuth.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("GetAuthorizationCookieResponse");

        _output.WriteLine($"Auth Response: {responseContent}");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ClientWebService_ShouldRejectNonPostRequests(string httpMethod)
    {
        // Arrange & Act
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), 
            await _fixture.GetFunctionUrl("ClientWebService/client.asmx"));
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    private static System.Xml.XmlNamespaceManager CreateNamespaceManager(System.Xml.XmlDocument doc)
    {
        var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        return nsmgr;
    }
}