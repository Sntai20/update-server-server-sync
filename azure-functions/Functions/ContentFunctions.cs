using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.ObjectModel;
using System.Net;

namespace MicrosoftUpdateFunctions.Functions
{
    public class ContentFunctions
    {
        private readonly ILogger _logger;
        private readonly IContentStore? _contentStore;

        public ContentFunctions(ILoggerFactory loggerFactory, IContentStore? contentStore = null)
        {
            _logger = loggerFactory.CreateLogger<ContentFunctions>();
            _contentStore = contentStore;
        }

        [Function("GetMicrosoftUpdateContent")]
        public async Task<HttpResponseData> GetContent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "content/{contentHash}")] HttpRequestData req,
            string contentHash)
        {
            _logger.LogInformation($"Content requested: {contentHash}");

            if (_contentStore == null)
            {
                _logger.LogWarning("No content store configured");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            try
            {
                ContentFileDigest parsedContentHash;
                try
                {
                    parsedContentHash = GetContentFileDigestFromUriPart(contentHash);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Invalid content hash format: {contentHash} - {ex.Message}");
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                if (_contentStore.Contains(parsedContentHash, out var fileName))
                {
                    // Log the range request if present
                    var rangeHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("Range", StringComparison.OrdinalIgnoreCase));
                    if (rangeHeader.Key != null)
                    {
                        _logger.LogInformation($"Requested {fileName}, range {rangeHeader.Value.FirstOrDefault()}");
                    }
                    else
                    {
                        _logger.LogInformation($"Requested {fileName}, no ranges");
                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    response.Headers.Add("Accept-Ranges", "bytes");

                    using var contentStream = _contentStore.Get(parsedContentHash);
                    
                    // Handle range requests
                    if (rangeHeader.Key != null && rangeHeader.Value.Any())
                    {
                        var rangeValue = rangeHeader.Value.First();
                        if (TryParseRange(rangeValue, contentStream.Length, out var start, out var end))
                        {
                            response.StatusCode = HttpStatusCode.PartialContent;
                            response.Headers.Add("Content-Range", $"bytes {start}-{end}/{contentStream.Length}");
                            
                            contentStream.Seek(start, SeekOrigin.Begin);
                            var buffer = new byte[end - start + 1];
                            await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            await response.WriteBytesAsync(buffer);
                        }
                        else
                        {
                            // Invalid range, return full content
                            await response.WriteBytesAsync(ReadAllBytes(contentStream));
                        }
                    }
                    else
                    {
                        // Return full content
                        await response.WriteBytesAsync(ReadAllBytes(contentStream));
                    }

                    return response;
                }
                else
                {
                    _logger.LogWarning($"Content not found: {contentHash}");
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error serving content: {contentHash}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetMicrosoftUpdateContentHead")]
        public async Task<HttpResponseData> GetContentHead(
            [HttpTrigger(AuthorizationLevel.Anonymous, "head", Route = "content/{contentHash}")] HttpRequestData req,
            string contentHash)
        {
            _logger.LogInformation($"HEAD request for content: {contentHash}");

            if (_contentStore == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            try
            {
                ContentFileDigest parsedContentHash;
                try
                {
                    parsedContentHash = GetContentFileDigestFromUriPart(contentHash);
                }
                catch (Exception)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                if (_contentStore.Contains(parsedContentHash, out var fileName))
                {
                    _logger.LogInformation($"HEAD {fileName}");

                    using var contentStream = _contentStore.Get(parsedContentHash);
                    
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Length", contentStream.Length.ToString());
                    response.Headers.Add("Accept-Ranges", "bytes");
                    
                    return response;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing HEAD request for content: {contentHash}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        private static byte[] HexStringToHex(string inputHex)
        {
            var resultantArray = new byte[inputHex.Length / 2];
            for (var i = 0; i < resultantArray.Length; i++)
            {
                resultantArray[i] = Convert.ToByte(inputHex.Substring(i * 2, 2), 16);
            }
            return resultantArray;
        }

        private static ContentFileDigest GetContentFileDigestFromUriPart(string name)
        {
            byte[] hashHex = HexStringToHex(name);
            if (hashHex.Length == 32)
            {
                return new ContentFileDigest("SHA256", Convert.ToBase64String(hashHex));
            }
            else if (hashHex.Length == 20)
            {
                return new ContentFileDigest("SHA1", Convert.ToBase64String(hashHex));
            }
            else
            {
                throw new ArgumentException("Name is not valid hash hex string", nameof(name));
            }
        }

        private static bool TryParseRange(string rangeValue, long contentLength, out long start, out long end)
        {
            start = 0;
            end = contentLength - 1;

            try
            {
                // Parse range header like "bytes=0-1023"
                if (rangeValue.StartsWith("bytes="))
                {
                    var range = rangeValue.Substring(6);
                    var parts = range.Split('-');
                    
                    if (parts.Length == 2)
                    {
                        if (!string.IsNullOrEmpty(parts[0]))
                        {
                            start = long.Parse(parts[0]);
                        }
                        
                        if (!string.IsNullOrEmpty(parts[1]))
                        {
                            end = long.Parse(parts[1]);
                        }
                        
                        // Validate range
                        if (start >= 0 && end < contentLength && start <= end)
                        {
                            return true;
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
    }
}