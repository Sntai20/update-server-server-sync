// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.PackageGraph.ObjectModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PackageGraph.Storage.Azure
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

        private BlobContentStore(BlobContainerClient contentContainer)
        {
            this.ParentContainer = contentContainer;
        }

        /// <summary>
        /// Opens an exiting or creates a new <see cref="IContentStore"/> with storage in the specified Azure Blob account and container
        /// </summary>
        /// <param name="client">The Azure Blob service client to use</param>
        /// <param name="containerName">The container name where to store update content</param>
        /// <returns></returns>
        public static BlobContentStore OpenOrCreate(BlobServiceClient client, string containerName)
        {
            var container = client.GetBlobContainerClient(containerName);
            container.CreateIfNotExists();

            return new BlobContentStore(container);
        }

        /// <inheritdoc cref="IContentStore.Download(IEnumerable{IContentFile}, CancellationToken)"/>
        public void Download(IEnumerable<IContentFile> files, CancellationToken cancelToken)
        {
            var queuedFiles = new List<IContentFile>();
            foreach (var file in files)
            {
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

                    if ((ulong)fileSizeOnServer != file.Size)
                    {
                        throw new Exception($"Mismatch in file size. Expected {file.Size}, server has {fileSizeOnServer}");
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

                        // Download block from source and upload to blob
                        using (var request = new HttpRequestMessage { RequestUri = new Uri(file.Source), Method = HttpMethod.Get })
                        {
                            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startOffset, startOffset + blockSize - 1);
                            using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken).GetAwaiter().GetResult();
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new Exception($"Failed to download block from {file.Source}: {response.ReasonPhrase}");
                            }

                            using var blockStream = response.Content.ReadAsStream(cancelToken);
                            fileBlob.StageBlock(blockId, blockStream);
                        }

                        blockIdList.Add(blockId);

                        if (cancellationSource.IsCancellationRequested)
                        {
                            break;
                        }

                        Interlocked.Add(ref this._DownloadedSize, blockSize);
                        progress.Current += blockSize;
                        this.Progress?.Invoke(this, progress);
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
                    throw new Exception($"Failed to get HEAD of update from {url}: {headResponse.ReasonPhrase}");
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
                throw new Exception("The requested file is not available");
            }
        }

        private BlockBlobClient GetBlobMarkerForFile(IContentFile updateFile)
        {
            return this.ParentContainer.GetBlockBlobClient(updateFile.Digest.HexString.ToLower() + ".complete");
        }

        private BlockBlobClient GetBlobForFile(IContentFile updateFile)
        {
            return this.ParentContainer.GetBlockBlobClient(updateFile.Digest.HexString.ToLower());
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
