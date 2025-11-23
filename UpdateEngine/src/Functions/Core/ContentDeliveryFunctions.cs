// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Core;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using System.Net;
using System.Text.Json;
using UpdateEngine.Functions.Shared;
using UpdateEngine.Core.Services;

/// <summary>
/// Consolidated functions for content delivery, downloads, and content management.
/// Combines functionality from ContentFunctions and DownloadFunctions.
/// </summary>
public class ContentDeliveryFunctions
{
    private readonly ILogger<ContentDeliveryFunctions> logger;
    private readonly IContentStore? contentStore;
    private readonly IQueryService queryService;
    private readonly JsonSerializerOptions jsonOptions;

    public ContentDeliveryFunctions(
        ILogger<ContentDeliveryFunctions> logger,
        IQueryService queryService,
        JsonSerializerOptions jsonOptions,
        IContentStore? contentStore = null)
    {
        this.logger = logger;
        this.contentStore = contentStore;
        this.queryService = queryService;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Direct content access by hash with range request support.
    /// GET /api/content/{contentHash}
    /// </summary>
    [Function("GetContent")]
    public async Task<HttpResponseData> GetContent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "content/{contentHash}")] HttpRequestData req,
        string contentHash)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            FunctionHelpers.ValidateRequiredParameters(
                ("contentHash", contentHash)
            );

            if (this.contentStore == null)
            {
                throw new NotSupportedException("Content store not configured");
            }

            var parsedContentHash = GetContentFileDigestFromUriPart(contentHash);
            
            if (!this.contentStore.Contains(parsedContentHash, out var fileName))
            {
                throw new FileNotFoundException($"Content not found: {contentHash}");
            }

            this.logger.LogInformation("Serving content: {FileName} for hash: {ContentHash}", fileName, contentHash);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/octet-stream");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            response.Headers.Add("Accept-Ranges", "bytes");

            using var contentStream = this.contentStore.Get(parsedContentHash);
            
            // Handle range requests
            var rangeHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("Range", StringComparison.OrdinalIgnoreCase));
            if (rangeHeader.Key != null && rangeHeader.Value.Any())
            {
                var rangeValue = rangeHeader.Value.First();
                if (TryParseRange(rangeValue, contentStream.Length, out var start, out var end))
                {
                    response.StatusCode = HttpStatusCode.PartialContent;
                    response.Headers.Add("Content-Range", $"bytes {start}-{end}/{contentStream.Length}");
                    
                    contentStream.Seek(start, SeekOrigin.Begin);
                    var buffer = new byte[end - start + 1];
                    await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    await response.WriteBytesAsync(buffer);
                }
                else
                {
                    await response.WriteBytesAsync(ReadAllBytes(contentStream));
                }
            }
            else
            {
                await response.WriteBytesAsync(ReadAllBytes(contentStream));
            }

            return response;
        });
    }

    /// <summary>
    /// HEAD request for content metadata and existence checking.
    /// HEAD /api/content/{contentHash}
    /// </summary>
    [Function("GetContentHead")]
    public async Task<HttpResponseData> GetContentHead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "head", Route = "content/{contentHash}")] HttpRequestData req,
        string contentHash)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            FunctionHelpers.ValidateRequiredParameters(
                ("contentHash", contentHash)
            );

            if (this.contentStore == null)
            {
                throw new NotSupportedException("Content store not configured");
            }

            var parsedContentHash = GetContentFileDigestFromUriPart(contentHash);
            
            if (!this.contentStore.Contains(parsedContentHash, out var fileName))
            {
                throw new FileNotFoundException($"Content not found: {contentHash}");
            }

            using var contentStream = this.contentStore.Get(parsedContentHash);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/octet-stream");
            response.Headers.Add("Content-Length", contentStream.Length.ToString());
            response.Headers.Add("Accept-Ranges", "bytes");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");

            return response;
        });
    }

    /// <summary>
    /// Download update metadata by ID in JSON format.
    /// GET /api/download/metadata/{updateId}
    /// </summary>
    [Function("DownloadMetadata")]
    public async Task<HttpResponseData> DownloadUpdateMetadata(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/metadata/{updateId}")] HttpRequestData req,
        string updateId)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            FunctionHelpers.ValidateRequiredParameters(
                ("updateId", updateId)
            );

            this.logger.LogInformation("Download metadata requested for update: {UpdateId}", updateId);

            var queryRequest = new MetadataQueryRequest
            {
                SearchTerm = updateId,
                MaxResults = 10,
                IncludeSuperseded = true
            };

            var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
            
            if (queryResult?.Packages?.Any() != true)
            {
                throw new FileNotFoundException($"Update not found: {updateId}");
            }

            // Try to find exact match by ID first, or take the first result
            var update = queryResult.Packages.FirstOrDefault(p => p.Id.ToString().Equals(updateId, StringComparison.OrdinalIgnoreCase))
                        ?? queryResult.Packages.First();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{updateId}_metadata.json\"");
            
            var json = JsonSerializer.Serialize(update, this.jsonOptions);
            await response.WriteStringAsync(json);
            return response;
        });
    }

    /// <summary>
    /// Download update content files by update ID.
    /// GET /api/download/content/{updateId}
    /// </summary>
    [Function("DownloadContent")]
    public async Task<HttpResponseData> DownloadUpdateContent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/content/{updateId}")] HttpRequestData req,
        string updateId)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync<object>(req, this.logger, async () =>
        {
            FunctionHelpers.ValidateRequiredParameters(
                ("updateId", updateId)
            );

            if (this.contentStore == null)
            {
                throw new NotSupportedException("Content store not configured");
            }

            this.logger.LogInformation("Download content requested for update: {UpdateId}", updateId);

            // TODO: Implement content download by update ID
            // This requires:
            // 1. Query metadata to get update details
            // 2. Extract file information from update metadata
            // 3. Retrieve content files from content store
            // 4. Package as ZIP or provide individual file access

            throw new NotImplementedException("Content download by update ID not yet implemented - requires detailed update file metadata access");
        });
    }

    /// <summary>
    /// List available downloads and their status.
    /// GET /api/download/list
    /// </summary>
    [Function("ListDownloads")]
    public async Task<HttpResponseData> ListUpdateDownloads(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/list")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var searchTerm = FunctionHelpers.GetQueryParameter(req, "search");
            var pageSize = int.TryParse(FunctionHelpers.GetQueryParameter(req, "pageSize"), out var ps) ? ps : 50;
            var pageNumber = int.TryParse(FunctionHelpers.GetQueryParameter(req, "page"), out var pn) ? pn : 1;

            var queryRequest = new MetadataQueryRequest
            {
                SearchTerm = searchTerm,
                MaxResults = pageSize
                // TODO: Implement pagination - MetadataQueryRequest doesn't support skip/offset yet
            };

            var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
            
            var downloadList = queryResult?.Packages?.Select(p => new
            {
                Id = p.Id,
                Title = p.Title,
                CreationDate = p.CreationDate,
                Size = p.Size,
                HasContent = this.contentStore != null // Basic availability check
            }) ?? Enumerable.Empty<object>();

            var result = new
            {
                Downloads = downloadList,
                Page = pageNumber,
                PageSize = pageSize,
                TotalResults = queryResult?.TotalMatches ?? 0
            };

            return result;
        });
    }

    /// <summary>
    /// Get content status and storage information.
    /// GET /api/content/status
    /// </summary>
    [Function("ContentStatus")]
    public async Task<HttpResponseData> GetContentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "content/status")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var status = new
            {
                ContentStoreAvailable = this.contentStore != null,
                ContentStoreType = this.contentStore?.GetType().Name ?? "Not configured",
                Timestamp = DateTime.UtcNow
            };

            return await Task.FromResult(status);
        });
    }

    #region Private Helper Methods

    private static ContentFileDigest GetContentFileDigestFromUriPart(string hashString)
    {
        // Remove any file extensions or extra parts
        var cleanHash = hashString.Split('.')[0];
        
        return cleanHash.Length switch
        {
            40 => new ContentFileDigest("sha1", Convert.ToBase64String(Convert.FromHexString(cleanHash))),
            64 => new ContentFileDigest("sha256", Convert.ToBase64String(Convert.FromHexString(cleanHash))),
            _ => throw new ArgumentException($"Invalid hash length: {cleanHash.Length}. Expected 40 (SHA1) or 64 (SHA256) characters.")
        };
    }

    private static bool TryParseRange(string rangeValue, long contentLength, out long start, out long end)
    {
        start = 0;
        end = contentLength - 1;

        try
        {
            if (rangeValue.StartsWith("bytes="))
            {
                var range = rangeValue.Substring(6);
                var parts = range.Split('-');
                
                if (parts.Length == 2)
                {
                    if (long.TryParse(parts[0], out start) && start >= 0)
                    {
                        if (string.IsNullOrEmpty(parts[1]))
                        {
                            end = contentLength - 1;
                        }
                        else if (long.TryParse(parts[1], out end) && end < contentLength)
                        {
                            // Range is valid
                        }
                        else
                        {
                            return false;
                        }
                        
                        return start <= end;
                    }
                }
            }
        }
        catch
        {
            // Invalid range format
        }

        return false;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    #endregion
}
