// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using Xunit;

public class MicrosoftUpdateTestFixture : IAsyncLifetime
{
    private readonly string baseAddress = "http://localhost:7071/api/";
    
    public HttpClient HttpClient { get; private set; } = null!;
    public string BaseAddress => this.baseAddress;

    public async Task InitializeAsync()
    {
        // For integration testing, we'll assume the Azure Functions are running locally
        // You can start them with: func start --port 7071
        HttpClient = new HttpClient { BaseAddress = new Uri(this.baseAddress) };
        
        // Add a small delay to allow functions to be ready
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        await Task.CompletedTask;
    }

    public async Task<string> GetFunctionUrl(string functionName)
    {
        return $"http://localhost:7071/api/{functionName}";
    }
}

[CollectionDefinition("MicrosoftUpdate")]
public class MicrosoftUpdateCollection : ICollectionFixture<MicrosoftUpdateTestFixture>
{
}
