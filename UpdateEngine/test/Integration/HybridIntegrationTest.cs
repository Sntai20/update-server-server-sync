// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using FluentAssertions;
using UpdateEngineTest.Infrastructure;
using System.Net;
using Xunit;

/// <summary>
/// Hybrid integration tests that can run with either test fixture approach
/// These tests demonstrate the flexibility of the testing infrastructure
/// </summary>
public class HybridIntegrationTest : IClassFixture<MicrosoftUpdateTestFixture>
{
    private readonly MicrosoftUpdateTestFixture _fixture;

    public HybridIntegrationTest(MicrosoftUpdateTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task System_ShouldBeReachable_ViaSimpleFixture()
    {
        // This test uses the simple fixture approach
        // It will work whether services are started manually or via AppHost
        
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.BaseAddress}ClientWebService/ClientWebService.asmx");
        
        // Should be reachable (method not allowed is fine for SOAP GET)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.MethodNotAllowed, 
            HttpStatusCode.OK, 
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Content_Endpoint_ShouldBeReachable()
    {
        // Test content endpoint accessibility
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.BaseAddress}content/test123");
        
        // 404 is expected for non-existent content, but endpoint should be reachable
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AllSoapEndpoints_ShouldBeReachable()
    {
        var endpoints = new[]
        {
            "ClientWebService/ClientWebService.asmx",
            "ServerSyncWebService/ServerSyncWebService.asmx",
            "SimpleAuthWebService/SimpleAuthWebService.asmx",
            "ReportingWebService/ReportingWebService.asmx"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.BaseAddress}{endpoint}");
            
            // All endpoints should be reachable
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.MethodNotAllowed,  // Expected for SOAP GET
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest);
        }
    }
}