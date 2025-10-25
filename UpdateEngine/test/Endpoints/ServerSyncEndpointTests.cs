// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Endpoints;

using FluentAssertions;
using UpdateEngineTest.Infrastructure;
using System.Net;
using Xunit;
using Xunit.Abstractions;

[Collection("MicrosoftUpdate")]
public class ServerSyncEndpointTests
{
    private readonly MicrosoftUpdateTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ServerSyncEndpointTests(MicrosoftUpdateTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ServerSyncWebService_ShouldRespondToGetConfigRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateServerSyncGetConfigRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("GetConfigData", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ServerSyncWebService/serversync.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("GetConfigDataResponse")
            .And.Contain("soap:Envelope");

        _output.WriteLine($"GetConfig Response: {responseContent}");
    }

    [Fact]
    public async Task ServerSyncWebService_ShouldRespondToGetRevisionIdListRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateGetRevisionIdListRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("GetRevisionIdList", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ServerSyncWebService/serversync.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("GetRevisionIdListResponse");

        _output.WriteLine($"GetRevisionIdList Response: {responseContent}");
    }

    [Fact]
    public async Task AuthenticationWebService_ShouldRespondToGetAuthConfigRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateGetAuthConfigRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("GetAuthConfig", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("AuthenticationWebService/DSSAuthWebService.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("GetAuthConfigResponse");

        _output.WriteLine($"GetAuthConfig Response: {responseContent}");
    }

    [Fact]
    public async Task ReportingWebService_ShouldRespondToReportEventBatchRequest()
    {
        // Arrange
        var soapBody = SoapTestHelper.CreateReportEventBatchRequest();
        var soapRequest = SoapTestHelper.CreateSoapRequest("ReportEventBatch", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ReportingWebService/reporting.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("ReportEventBatchResponse");

        _output.WriteLine($"ReportEventBatch Response: {responseContent}");
    }

    [Fact]
    public async Task ServerSyncWebService_ShouldHandleUnsupportedSoapActions()
    {
        // Arrange
        var soapBody = "<UnsupportedOperation xmlns='http://www.microsoft.com/SoftwareDistribution/Server/ServerSync' />";
        var soapRequest = SoapTestHelper.CreateSoapRequest("UnsupportedOperation", soapBody);

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ServerSyncWebService/serversync.asmx"), 
            soapRequest);

        // Assert  
        // The response could be either a SOAP fault or method not found, both are acceptable
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

        var responseContent = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            responseContent.Should().Contain("soap:Fault");
        }

        _output.WriteLine($"Unsupported Operation Response: {responseContent}");
    }

    [Fact]
    public async Task ServerSyncWebService_ShouldValidateSoapEnvelope()
    {
        // Arrange
        var malformedSoap = @"<?xml version='1.0' encoding='utf-8'?>
<soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
    <soap:Body>
        <!-- Missing proper operation -->
    </soap:Body>
</soap:Envelope>";

        var soapRequest = new StringContent(malformedSoap, System.Text.Encoding.UTF8, "text/xml");
        soapRequest.Headers.Add("SOAPAction", "http://www.microsoft.com/SoftwareDistribution/Server/ServerSync/GetConfig");

        // Act
        var response = await _fixture.HttpClient.PostAsync(
            await _fixture.GetFunctionUrl("ServerSyncWebService/serversync.asmx"), 
            soapRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("soap:Fault");

        _output.WriteLine($"Malformed SOAP Response: {responseContent}");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ServerSyncWebService_ShouldRejectNonPostRequests(string httpMethod)
    {
        // Arrange & Act
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), 
            await _fixture.GetFunctionUrl("ServerSyncWebService/serversync.asmx"));
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}