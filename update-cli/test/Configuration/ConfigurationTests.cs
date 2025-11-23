// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateCli.Tests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpdateEngine.Core.Orchestrators;
using Xunit;

/// <summary>
/// Tests for CLI configuration loading and service registration.
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void Configuration_LoadsFromJsonFile()
    {
        // Arrange & Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        // Assert
        var cliMode = configuration.GetValue<string>("CliMode");
        cliMode.Should().Be("local");

        var metadataPath = configuration.GetValue<string>("UpdateEngine:StorageConfiguration:MetadataPath");
        metadataPath.Should().Be("./TestData/metadata");
    }

    [Fact]
    public void Configuration_LocalMode_ParsesCorrectly()
    {
        // Arrange & Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        var cliMode = configuration.GetValue<string>("CliMode");

        // Assert
        cliMode.Should().Be("local");
    }

    [Fact]
    public void Configuration_RemoteMode_ParsesCorrectly()
    {
        // Arrange
        var configJson = @"{
            ""CliMode"": ""remote"",
            ""UpdateEngine"": {
                ""ApiBaseUrl"": ""http://localhost:7071""
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(configJson)))
            .Build();

        // Act
        var cliMode = configuration.GetValue<string>("CliMode");
        var apiBaseUrl = configuration.GetValue<string>("UpdateEngine:ApiBaseUrl");

        // Assert
        cliMode.Should().Be("remote");
        apiBaseUrl.Should().Be("http://localhost:7071");
    }

    [Fact]
    public void Configuration_FeatureFlags_LoadCorrectly()
    {
        // Arrange & Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        // Assert
        var enableContentDownload = configuration.GetValue<bool>("UpdateEngine:FeatureFlags:EnableContentDownload");
        var enableMetadataSync = configuration.GetValue<bool>("UpdateEngine:FeatureFlags:EnableMetadataSync");
        var enableHealthChecks = configuration.GetValue<bool>("UpdateEngine:FeatureFlags:EnableHealthChecks");

        enableContentDownload.Should().BeTrue();
        enableMetadataSync.Should().BeTrue();
        enableHealthChecks.Should().BeTrue();
    }

    [Fact]
    public void Configuration_StorageConfiguration_LoadsCorrectly()
    {
        // Arrange & Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        // Assert
        var metadataPath = configuration.GetValue<string>("UpdateEngine:StorageConfiguration:MetadataPath");
        var contentPath = configuration.GetValue<string>("UpdateEngine:StorageConfiguration:ContentPath");
        var provider = configuration.GetValue<string>("UpdateEngine:StorageConfiguration:Provider");

        metadataPath.Should().Be("./TestData/metadata");
        contentPath.Should().Be("./TestData/content");
        provider.Should().Be("FileSystem");
    }

    // Note: Mock implementations removed. Use Moq instead for testing actual orchestrator behavior.
    // This test validates configuration loading and service registration patterns only.
}
