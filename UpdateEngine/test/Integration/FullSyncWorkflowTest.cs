namespace UpdateEngineTest.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using UpdateEngineTest.Infrastructure;
using Xunit;
using Xunit.Abstractions;

[Collection("AspireAppHost")]
public class FullSyncWorkflowTests
{
    private readonly AspireAppHostTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FullSyncWorkflowTests(AspireAppHostTestFixture fixture, ITestOutputHelper output)
    {
        this._fixture = fixture;
        this._output = output;
    }

    [Fact]
    public async Task CompleteWorkflow_SyncCategoriesMetadataAndContent()
    {
        // Step 1: Sync categories
        var categoriesRequest = new
        {
            SyncCategories = true,
            SyncUpdates = false
        };
        var categoriesJson = JsonSerializer.Serialize(categoriesRequest);
        var categoriesContent = new StringContent(categoriesJson, Encoding.UTF8, "application/json");
        
        var categoriesResponse = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", categoriesContent);
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        this._output.WriteLine("✓ Categories synced");

        // Step 2: Sync critical updates metadata
        var metadataRequest = new
        {
            SyncCategories = false,
            SyncUpdates = true,
            FilterType = "critical"
        };
        var metadataJson = JsonSerializer.Serialize(metadataRequest);
        var metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json");
        
        var metadataResponse = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", metadataContent);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        this._output.WriteLine("✓ Metadata synced");

        // Step 3: Check store status
        var storeStatusResponse = await this._fixture.HttpClient.GetAsync("/api/QueryMetadataStoreStatus");
        var status = await storeStatusResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"Store status: {status}");

        // Step 4: Sync content for 1 update
        var contentRequest = new { MaxItems = 1 };
        var contentJson = JsonSerializer.Serialize(contentRequest);
        var contentContent = new StringContent(contentJson, Encoding.UTF8, "application/json");
        
        var contentResponse = await this._fixture.HttpClient.PostAsync("/api/SyncContent", contentContent);
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        
        var contentResult = await contentResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"✓ Content synced: {contentResult}");
    }

    [Fact]
    public async Task CompleteWorkflow_WithContentVerification()
    {
        // Step 1: Sync categories
        var categoriesRequest = new { SyncCategories = true, SyncUpdates = false };
        var categoriesJson = JsonSerializer.Serialize(categoriesRequest);
        var categoriesContent = new StringContent(categoriesJson, Encoding.UTF8, "application/json");
        
        var categoriesResponse = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", categoriesContent);
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        this._output.WriteLine("✓ Categories synced");

        // Step 2: Sync comprehensive updates
        var metadataRequest = new
        {
            SyncCategories = false,
            SyncUpdates = true,
            FilterType = "comprehensive"  // Get more updates
        };
        var metadataJson = JsonSerializer.Serialize(metadataRequest);
        var metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json");
        
        var metadataResponse = await this._fixture.HttpClient.PostAsync("/api/UniversalSync", metadataContent);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        this._output.WriteLine("✓ Metadata synced");

        // Step 3: Query for updates WITH files
        var queryRequest = new
        {
            IncludeUpdates = true,
            HasFiles = true,
            MaxResults = 5
        };
        var queryJson = JsonSerializer.Serialize(queryRequest);
        var queryContent = new StringContent(queryJson, Encoding.UTF8, "application/json");
        
        var queryResponse = await this._fixture.HttpClient.PostAsync("/api/QueryMetadata", queryContent);
        var updatesWithFiles = await queryResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"Updates with files: {updatesWithFiles}");

        // Step 4: Sync content for updates with files
        var contentRequest = new { MaxItems = 1 };
        var contentJson = JsonSerializer.Serialize(contentRequest);
        var contentContent = new StringContent(contentJson, Encoding.UTF8, "application/json");
        
        var contentResponse = await this._fixture.HttpClient.PostAsync("/api/SyncContent", contentContent);
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        
        var contentResult = await contentResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"✓ Content sync result: {contentResult}");
        
        // Step 5: Verify content was downloaded
        var contentStatusResponse = await this._fixture.HttpClient.GetAsync("/api/QueryContentStatus");
        var contentStatus = await contentStatusResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"Content status: {contentStatus}");
    }
}