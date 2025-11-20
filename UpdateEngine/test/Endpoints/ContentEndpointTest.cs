// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Endpoints;

using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using UpdateEngineTest.Infrastructure;
using Xunit;
using Xunit.Abstractions;

[Collection("MicrosoftUpdate")]
public class ContentEndpointTest
{
    private readonly MicrosoftUpdateTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ContentEndpointTest(MicrosoftUpdateTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [InlineData("sha1")]
    [InlineData("sha256")]
    public async Task Content_ShouldReturnNotFoundForNonExistentContent(string hashType)
    {
        // Arrange
        var testHash = hashType == "sha1" ? 
            "da39a3ee5e6b4b0d3255bfef95601890afd80709" : // SHA1 of empty string
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; // SHA256 of empty string

        // Act
        var response = await _fixture.HttpClient.GetAsync(
            await _fixture.GetFunctionUrl($"content/{testHash}"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _output.WriteLine($"GET {hashType}/{testHash}: {response.StatusCode}");
    }

    [Theory]
    [InlineData("sha1")]
    [InlineData("sha256")]
    public async Task Content_HeadShouldReturnNotFoundForNonExistentContent(string hashType)
    {
        // Arrange
        var testHash = hashType == "sha1" ? 
            "da39a3ee5e6b4b0d3255bfef95601890afd80709" : 
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var request = new HttpRequestMessage(HttpMethod.Head, 
            await _fixture.GetFunctionUrl($"content/{testHash}"));

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().BeEmpty(); // HEAD responses should have no body

        _output.WriteLine($"HEAD {hashType}/{testHash}: {response.StatusCode}");
    }

    [Fact]
    public async Task Content_ShouldRejectInvalidHashFormats()
    {
        // Arrange
        var invalidHashes = new[]
        {
            "invalid-hash",
            "too-short",
            "gggggggggggggggggggggggggggggggggggggggg", // Invalid hex characters
            "da39a3ee5e6b4b0d3255bfef95601890afd8070", // SHA1 too short
            "da39a3ee5e6b4b0d3255bfef95601890afd80709aa" // SHA1 too long
        };

        foreach (var hash in invalidHashes)
        {
            // Act
            var response = await _fixture.HttpClient.GetAsync(
                await _fixture.GetFunctionUrl($"content/{hash}"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            _output.WriteLine($"Invalid hash {hash}: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Content_ShouldHandleRangeRequestsCorrectly()
    {
        // Arrange
        var testHash = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        var request = new HttpRequestMessage(HttpMethod.Get, 
            await _fixture.GetFunctionUrl($"content/{testHash}"));
        request.Headers.Range = new RangeHeaderValue(0, 100);

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        // Should return 404 since content doesn't exist, but range header should be processed
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _output.WriteLine($"Range request for {testHash}: {response.StatusCode}");
    }

    [Fact]
    public async Task Content_ShouldSupportMultipleRangeFormats()
    {
        // Arrange
        var testHash = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        var rangeHeaders = new[]
        {
            "bytes=0-100",
            "bytes=200-",
            "bytes=-100",
            "bytes=0-100,200-300"
        };

        foreach (var rangeHeader in rangeHeaders)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                await _fixture.GetFunctionUrl($"content/{testHash}"));
            request.Headers.Add("Range", rangeHeader);

            // Act
            var response = await _fixture.HttpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            _output.WriteLine($"Range {rangeHeader}: {response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Content_ShouldRejectUnsupportedHttpMethods(string httpMethod)
    {
        // Arrange
        var testHash = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), 
            await _fixture.GetFunctionUrl($"content/{testHash}"));

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        _output.WriteLine($"{httpMethod} method: {response.StatusCode}");
    }

    [Fact]
    public async Task Content_ShouldHandleInvalidHashLengths()
    {
        // Arrange
        var invalidHashLengths = new[] { 
            "da39a3ee5e6b4b0d", // Too short
            "da39a3ee5e6b4b0d3255bfef95601890afd80709aa" // Too long for SHA1
        };

        foreach (var hash in invalidHashLengths)
        {
            // Act
            var response = await _fixture.HttpClient.GetAsync(
                await _fixture.GetFunctionUrl($"content/{hash}"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            _output.WriteLine($"Invalid hash length {hash}: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Content_ShouldSetCorrectResponseHeaders()
    {
        // Arrange
        var testHash = "da39a3ee5e6b4b0d3255bfef95601890afd80709";

        // Act
        var response = await _fixture.HttpClient.GetAsync(
            await _fixture.GetFunctionUrl($"content/{testHash}"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        // Even for 404 responses, check that we're not exposing inappropriate headers
        response.Headers.Should().NotContain(h => h.Key == "Server");
        
        _output.WriteLine($"Response headers: {string.Join(", ", response.Headers.Select(h => h.Key))}");
    }

    [Fact]
    public async Task Content_ShouldHandleCaseSensitiveHashes()
    {
        // Arrange - SHA1 hashes should be case-insensitive
        var lowerCaseHash = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        var upperCaseHash = "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709";

        // Act
        var lowerResponse = await _fixture.HttpClient.GetAsync(
            await _fixture.GetFunctionUrl($"content/{lowerCaseHash}"));
        var upperResponse = await _fixture.HttpClient.GetAsync(
            await _fixture.GetFunctionUrl($"content/{upperCaseHash}"));

        // Assert
        // Both should behave the same (404 since content doesn't exist)
        lowerResponse.StatusCode.Should().Be(upperResponse.StatusCode);

        _output.WriteLine($"Lowercase: {lowerResponse.StatusCode}, Uppercase: {upperResponse.StatusCode}");
    }
}