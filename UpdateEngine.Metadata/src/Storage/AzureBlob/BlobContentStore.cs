// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using UpdateEngine.Metadata.ObjectModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UpdateEngine.Metadata.Storage.Azure
{
    /// <summary>
    /// Implementation of <see cref="IContentStore"/> that downloads and stores update content in Azure Blob Storage
    /// </summary>
    public class BlobContentStore : IContentStore
    {
        /// <inheritdoc cref="IContentStore.Progress"/>
        public event EventHandler<ContentOperationProgress> Progress;

        private const long BlockSize = 64 * 1024 * 1024;

        readonly BlobContainerClient ParentContainer;
        readonly string PathPrefix;

        /// <summary>
        /// List of pending downloads
        /// </summary>
        public ConcurrentDictionary<string, IContentFile> PendingFileDownloads = new();

        /// <inheritdoc cref="IContentStore.QueuedSize"/>
        public long QueuedSize => _QueuedSize;

        /// <inheritdoc cref="IContentStore.DownloadedSize"/>
        public long DownloadedSize => _DownloadedSize;

        /// <inheritdoc cref="IContentStore.QueuedCount"/>
        public int QueuedCount => _QueuedCount;

        long _QueuedSize;
        long _DownloadedSize;
        int _QueuedCount;

        private BlobContentStore(BlobContainerClient contentContainer, string pathPrefix = "")
        {
            this.ParentContainer = contentContainer;
            this.PathPrefix = pathPrefix;
        }

        /// <summary>
        /// Opens an exiting or creates a new <see cref="IContentStore"/> with storage in the specified Azure Blob account and container
        /// </summary>
        /// <param name="client">The Azure Blob service client to use</param>
        /// <param name="containerName">The container name where to store update content</param>
        /// <param name="pathPrefix">Optional path prefix for organizing blobs within the container</param>
        /// <returns></returns>
        public static BlobContentStore OpenOrCreate(BlobServiceClient client, string containerName, string pathPrefix = "")
        {
            var container = client.GetBlobContainerClient(containerName);
            container.CreateIfNotExists();

            return new BlobContentStore(container, pathPrefix);
        }

        /// <inheritdoc cref="IContentStore.Download(IEnumerable{IContentFile}, CancellationToken)"/>
        public void Download(IEnumerable<IContentFile> files, CancellationToken cancelToken)
        {
            var queuedFiles = new List<IContentFile>();
            foreach (var file in files)
            {
                // Skip files without a valid source URL
                if (string.IsNullOrEmpty(file.Source))
                {
                    // Skip silently - this is expected for many file types (ARM64, FoD, metadata, etc.)
                    continue;
                }

                if (this.PendingFileDownloads.TryAdd(file.Source, file))
                {
                    queuedFiles.Add(file);
                }
            }

            Interlocked.Add(ref this._QueuedCount, queuedFiles.Count);

            Interlocked.Add(ref this._QueuedSize, queuedFiles.Sum(f => (long)f.Size));

            var cancellationSource = new CancellationTokenSource();
            var progress = new ContentOperationProgress();


            this.Progress?.Invoke(this, progress);


            foreach (var file in queuedFiles)
            {
                // Additional safety check for digest
                if (file.Digest == null)
                {
                    // Skip silently - missing digest means we can't verify file integrity
                    Interlocked.Add(ref this._QueuedSize, (long)file.Size * -1);
                    Interlocked.Decrement(ref this._QueuedCount);
                    this.PendingFileDownloads.TryRemove(file.Source, out _);
                    continue;
                }

                progress.Maximum = (long)file.Size;
                progress.CurrentOperation = PackagesOperationType.DownloadFileStart;
                this.Progress?.Invoke(this, progress);

                if (this.Contains(file))
                {
                    Interlocked.Add(ref this._DownloadedSize, (long)file.Size);
                    Interlocked.Decrement(ref this._QueuedCount);

                    progress.Current = (long)file.Size;
                    progress.CurrentOperation = PackagesOperationType.DownloadFileEnd;
                    this.Progress?.Invoke(this, progress);

                    this.PendingFileDownloads.TryRemove(file.Source, out var completeFileRemoved);

                    continue;
                }

                progress.CurrentOperation = PackagesOperationType.DownloadFileProgress;
                var fileBlob = this.GetBlobForFile(file);

                using (var client = new HttpClient())
                {
                    var fileSizeOnServer = GetFileSizeOnSourceServer(client, file.Source, cancelToken);
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (fileSizeOnServer != (long)file.Size)
                    {
                        throw new InvalidDataException($"Mismatch in file size. Expected {file.Size}, server has {fileSizeOnServer}");
                    }

                    int startBlock = 0;
                    var blockCount = fileSizeOnServer / BlockSize + (fileSizeOnServer % BlockSize == 0 ? 0 : 1);
                    List<string> blockIdList;

                    if (fileBlob.Exists())
                    {
                        var blockListResponse = fileBlob.GetBlockList(BlockListTypes.Uncommitted);
                        var fileBlocks = blockListResponse.Value.UncommittedBlocks.ToList();

                        if (fileBlocks.Count <= blockCount)
                        {
                            startBlock = fileBlocks.Count;
                        }

                        blockIdList = fileBlocks.Select(b => b.Name).ToList();
                    }
                    else
                    {
                        blockIdList = new List<string>();
                    }

                    for (int i = startBlock; i < blockCount; i++)
                    {
                        var startOffset = i * BlockSize;
                        var blockSize = (fileSizeOnServer % BlockSize != 0 && i == (blockCount - 1) ? fileSizeOnServer % BlockSize : BlockSize);

                        var blockId = Convert.ToBase64String(BitConverter.GetBytes(i));

                        // Retry logic for downloading block from source
                        const int maxRetries = 3;
                        var retryDelay = TimeSpan.FromSeconds(2);
                        Exception lastException = null;
                        
                        for (int retry = 0; retry < maxRetries; retry++)
                        {
                            try
                            {
                                // Download block from source and upload to blob
                                using (var request = new HttpRequestMessage { RequestUri = new Uri(file.Source), Method = HttpMethod.Get })
                                {
                                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startOffset, startOffset + blockSize - 1);
                                    
                                    // Set timeout for this request (5 minutes for large blocks)
                                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCts.Token);
                                    
                                    using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).GetAwaiter().GetResult();
                                    if (!response.IsSuccessStatusCode)
                                    {
                                        throw new HttpRequestException($"Failed to download block {i}/{blockCount} from {file.Source}: {response.ReasonPhrase}");
                                    }

                                    using var httpStream = response.Content.ReadAsStream(linkedCts.Token);
                                    
                                    // Buffer the stream content to support Length property for Azure Blob staging
                                    // Use larger buffer for 64MB blocks
                                    using var bufferedStream = new MemoryStream((int)blockSize);
                                    
                                    // Copy with progress tracking and cancellation support
                                    var buffer = new byte[81920]; // 80KB buffer
                                    int bytesRead;
                                    long totalBytesRead = 0;
                                    
                                    while ((bytesRead = httpStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        linkedCts.Token.ThrowIfCancellationRequested();
                                        bufferedStream.Write(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;
                                        
                                        // Report progress for large blocks
                                        if (totalBytesRead % (10 * 1024 * 1024) == 0) // Every 10MB
                                        {
                                            Console.WriteLine($"[BlobContentStore] Downloaded {totalBytesRead / (1024 * 1024)}MB of block {i}/{blockCount}");
                                        }
                                    }
                                    
                                    if (totalBytesRead != blockSize)
                                    {
                                        throw new HttpRequestException($"Downloaded {totalBytesRead} bytes but expected {blockSize} bytes for block {i}/{blockCount}");
                                    }
                                    
                                    bufferedStream.Position = 0; // Reset position for reading
                                    
                                    fileBlob.StageBlock(blockId, bufferedStream);
                                    
                                    Console.WriteLine($"[BlobContentStore] Successfully staged block {i}/{blockCount} ({blockSize / (1024 * 1024)}MB)");
                                }

                                blockIdList.Add(blockId);

                                if (cancellationSource.IsCancellationRequested)
                                {
                                    break;
                                }

                                Interlocked.Add(ref this._DownloadedSize, blockSize);
                                progress.Current += blockSize;
                                this.Progress?.Invoke(this, progress);
                                
                                // Success - break retry loop
                                break;
                            }
                            catch (Exception ex) when (ex is HttpRequestException || 
                                                       ex is TaskCanceledException || 
                                                       ex is OperationCanceledException ||
                                                       ex is System.Net.Http.HttpIOException)
                            {
                                lastException = ex;
                                
                                if (retry < maxRetries - 1)
                                {
                                    Console.WriteLine($"[BlobContentStore] Block {i}/{blockCount} download failed (attempt {retry + 1}/{maxRetries}): {ex.Message}. Retrying in {retryDelay.TotalSeconds}s...");
                                    System.Threading.Thread.Sleep(retryDelay);
                                    retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2); // Exponential backoff
                                }
                                else
                                {
                                    Console.WriteLine($"[BlobContentStore] Block {i}/{blockCount} download failed after {maxRetries} attempts: {ex.Message}");
                                    throw new HttpRequestException($"Failed to download block {i}/{blockCount} after {maxRetries} attempts. Last error: {ex.Message}", ex);
                                }
                            }
                        }
                    }

                    fileBlob.CommitBlockList(blockIdList);

                    // Write marker file
                    var markerBlob = this.GetBlobMarkerForFile(file);
                    using var markerStream = new MemoryStream(Convert.FromBase64String(file.Digest.DigestBase64));
                    markerBlob.Upload(markerStream);

                }

                Interlocked.Add(ref this._DownloadedSize, (long)file.Size * -1);
                Interlocked.Add(ref this._QueuedSize, (long)file.Size * -1);
                Interlocked.Decrement(ref this._QueuedCount);
                progress.CurrentOperation = PackagesOperationType.DownloadFileEnd;
                this.Progress?.Invoke(this, progress);

                this.PendingFileDownloads.TryRemove(file.Source, out var downloadedFileRemoved);
            }
        }

        private static long GetFileSizeOnSourceServer(HttpClient client, string url, CancellationToken cancellationToken)
        {
            // First get the HEAD to check the server's size for the file
            long fileSizeOnServer;
            using (var request = new HttpRequestMessage { RequestUri = new Uri(url), Method = HttpMethod.Head })
            {
                using var headResponse = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();
                if (!headResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Failed to get HEAD of update from {url}: {headResponse.ReasonPhrase}");
                }

                fileSizeOnServer = headResponse.Content.Headers.ContentLength.Value;
            }

            return fileSizeOnServer;
        }

        /// <inheritdoc cref="IContentStore.Contains(IContentFile)"/>
        public bool Contains(IContentFile file)
        {
            return this.GetBlobMarkerForFile(file).Exists();
        }

        /// <inheritdoc cref="IContentStore.Get(IContentFile)"/>
        public Stream Get(IContentFile contentFile)
        {
            var doneMarker = this.GetBlobMarkerForFile(contentFile);
            if (doneMarker.Exists())
            {
                var fileBlob = this.GetBlobForFile(contentFile);
                return fileBlob.OpenRead();
            }
            else
            {
                throw new FileNotFoundException("The requested file is not available");
            }
        }

        private BlockBlobClient GetBlobMarkerForFile(IContentFile updateFile)
        {
            var markerName = string.IsNullOrEmpty(this.PathPrefix) 
                ? updateFile.Digest.HexString.ToLower() + ".complete"
                : $"{this.PathPrefix.TrimEnd('/')}/{updateFile.Digest.HexString.ToLower()}.complete";
                
            return this.ParentContainer.GetBlockBlobClient(markerName);
        }

        private BlockBlobClient GetBlobForFile(IContentFile updateFile)
        {
            var blobName = string.IsNullOrEmpty(this.PathPrefix) 
                ? updateFile.Digest.HexString.ToLower()
                : $"{this.PathPrefix.TrimEnd('/')}/{updateFile.Digest.HexString.ToLower()}";
            
            // Debug logging to track blob path construction
            Console.WriteLine($"[BlobContentStore] Creating blob path: '{blobName}' (PathPrefix: '{this.PathPrefix}', Hash: '{updateFile.Digest.HexString}')");
            
            return this.ParentContainer.GetBlockBlobClient(blobName);
        }

        /// <inheritdoc cref="IContentStore.GetUri(IContentFile)"/>
        public string GetUri(IContentFile updateFile)
        {
            var fileBlob = this.GetBlobForFile(updateFile);

            // Check if blob has user delegation SAS support (requires Entra ID auth)
            // For storage account key auth, fall back to account SAS
            if (this.ParentContainer.CanGenerateSasUri)
            {
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = fileBlob.BlobContainerName,
                    BlobName = fileBlob.Name,
                    Resource = "b", // Blob
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var sasUri = fileBlob.GenerateSasUri(sasBuilder);
                return sasUri.ToString();
            }
            else
            {
                // Fallback: return blob URL without SAS (assumes public access or caller has auth)
                return fileBlob.Uri.ToString();
            }
        }

        /// <inheritdoc cref="IContentStore.DownloadAsync(IContentFile, CancellationToken)"/>
        public Task DownloadAsync(IContentFile file, CancellationToken cancelToken)
        {
            var downloadTask = new Task(() =>
           {
               this.Download(new List<IContentFile>() { file }, cancelToken);
           });

            downloadTask.Start();

            return downloadTask;
        }

        /// <inheritdoc cref="IContentStore.Contains(IContentFileDigest, out string)"/>
        public bool Contains(IContentFileDigest fileDigest, out string fileName)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc cref="IContentStore.Get(IContentFileDigest)"/>
        public Stream Get(IContentFileDigest fileDigest)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc cref="IContentStore.GetUri(IContentFileDigest)"/>
        public string GetUri(IContentFileDigest fileDigest)
        {
            throw new NotImplementedException();
        }
    }
}
