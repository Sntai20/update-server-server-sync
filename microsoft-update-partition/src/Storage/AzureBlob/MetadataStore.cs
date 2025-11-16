// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Partitions;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.PackageGraph.Storage.Azure
{
    class MetadataStore
    {
        readonly BlobContainerClient Container;

        private const long InitialBlobSize = 32 * 1024 * 1024;
        private const int MetadataPageSize = 512;

        private long NextAvailableOffset;
        private long PageBlobSize;

        private const string MetadataBlobName = "metadata";

        private readonly object MetadataBlobLock = new();

        private const int UploadCacheSize = 32 * 1024 * 1024;
        private readonly MemoryStream UploadCache = new(UploadCacheSize);
        private long UploadCacheOffset = 0;

        private readonly EventWaitHandle BackBufferReadyEvent = new(true, EventResetMode.AutoReset);

        private const int DownloadCacheSize = 4 * 1024 * 1024;
        private long DownloadCacheOffset = long.MaxValue;
        private readonly MemoryStream DownloadCache = new(DownloadCacheSize);

        internal MetadataStore(BlobContainerClient container)
        {
            this.Container = container;
            // Create PageBlobClient using the container client and blob name
            var blobClient = this.Container.GetBlobClient(MetadataBlobName);
            var targetBlob = blobClient.WithSnapshot(null).GetParentBlobContainerClient().GetPageBlobClient(MetadataBlobName);

            if (!targetBlob.Exists())
            {
                targetBlob.Create(InitialBlobSize);
                this.UploadCacheOffset = this.NextAvailableOffset = 0;
                this.PageBlobSize = 0;
            }
            else
            {
                var pageRanges = targetBlob.GetPageRanges();
                var ranges = pageRanges.Value.PageRanges.ToList();
                // PageRangeItem has Start and End properties (not Range property)
                this.UploadCacheOffset = this.NextAvailableOffset = !ranges.Any() ? 0 : ranges.Max(r => r.Offset + (r.Length ?? 0));
                this.PageBlobSize = targetBlob.GetProperties().Value.ContentLength;
            }
        }

        private PageBlobClient GetPageBlobClient()
        {
            return this.Container.GetBlobClient(MetadataBlobName).WithSnapshot(null).GetParentBlobContainerClient().GetPageBlobClient(MetadataBlobName);
        }

        private void FillReadCache(long startOffset, long requiredLength)
        {
            this.DownloadCache.Seek(0, SeekOrigin.Begin);
            var targetBlob = this.GetPageBlobClient();

            if (startOffset > this.NextAvailableOffset)
            {
                throw new Exception("Download offset cannot be past the end of the page blob");
            }

            var fillSize = Math.Max(requiredLength, DownloadCacheSize);
            fillSize = Math.Min(fillSize, this.NextAvailableOffset - startOffset);

            if (fillSize < requiredLength)
            {
                throw new Exception("Not enought range avaialable in the metadata blob");
            }

            this.DownloadCache.Seek(0, SeekOrigin.Begin);
            var downloadInfo = targetBlob.Download(new HttpRange(startOffset, fillSize));
            downloadInfo.Value.Content.CopyTo(this.DownloadCache);
            this.DownloadCache.Seek(0, SeekOrigin.Begin);
            this.DownloadCache.SetLength(fillSize);
            this.DownloadCacheOffset = startOffset;
        }

        private void FillBufferForPackage(PackageStoreEntry packageEntry)
        {
            lock (this.DownloadCache)
            {
                var minBufferRequired = (packageEntry.FileListOffset - packageEntry.MetadataOffset) + packageEntry.FileListLength;

                if (packageEntry.MetadataOffset < this.DownloadCacheOffset ||
          packageEntry.MetadataOffset + minBufferRequired >= this.DownloadCacheOffset + this.DownloadCache.Length)
                {
                    this.FillReadCache(packageEntry.MetadataOffset, minBufferRequired);
                }
            }
        }

        public Stream GetMetadata(PackageStoreEntry packageEntry)
        {
            lock (this.DownloadCache)
            {
                this.FillBufferForPackage(packageEntry);

                var cachedPackageBuffer = new byte[packageEntry.MetadataLength];
                this.DownloadCache.Seek(packageEntry.MetadataOffset - this.DownloadCacheOffset, SeekOrigin.Begin);
                this.DownloadCache.Read(cachedPackageBuffer, 0, cachedPackageBuffer.Length);
                return new GZipStream(new MemoryStream(cachedPackageBuffer), CompressionMode.Decompress);
            }
        }

        public List<T> GetFiles<T>(PackageStoreEntry packageEntry)
        {
            if (packageEntry.FileListLength == 0)
            {
                return new List<T>();
            }

            Stream inMemoryFilesList;
            lock (this.DownloadCache)
            {
                this.FillBufferForPackage(packageEntry);

                var cachedFileListBuffer = new byte[packageEntry.FileListLength];
                this.DownloadCache.Seek(packageEntry.FileListOffset - this.DownloadCacheOffset, SeekOrigin.Begin);
                this.DownloadCache.Read(cachedFileListBuffer);
                inMemoryFilesList = new GZipStream(new MemoryStream(cachedFileListBuffer), CompressionMode.Decompress);
            }

            var filesList = JsonSerializer.Deserialize<List<T>>(inMemoryFilesList);

            return filesList;
        }

        private static long RoundToPageSize(long value) => value % MetadataPageSize == 0 ? value : MetadataPageSize * (value / MetadataPageSize) + MetadataPageSize;

        private static MemoryStream CreateFileMetadataStream(IPackage package)
        {
            var filesMetadata = new MemoryStream();
            JsonSerializer.Serialize(filesMetadata, package.Files);

            filesMetadata.Seek(0, SeekOrigin.Begin);
            return filesMetadata;
        }

        private static bool PackageHasExternalFileMetadata(IPackage package)
        {
            return (PartitionRegistration.TryGetPartitionFromPackage(package, out var partitionDefinition) &&
             partitionDefinition.HasExternalContentFileMetadata &&
               package.Files != null &&
            package.Files.Any());
        }

        public PackageStoreEntry AddPackage(IPackage package)
        {
            PackageStoreEntry newEntry = new(package.Id.ToString(), 0);

            using var uploadStream = new MemoryStream();
            using (var compressor = new GZipStream(uploadStream, CompressionLevel.Optimal, true))
            {
                package.GetMetadataStream().CopyTo(compressor);
            }

            newEntry.MetadataLength = uploadStream.Length;

            uploadStream.SetLength(RoundToPageSize(uploadStream.Length));

            var filesMetadataRelativeOffset = uploadStream.Length;

            if (PackageHasExternalFileMetadata(package))
            {
                var filesMetadata = new MemoryStream();
                using (var compressor = new GZipStream(filesMetadata, CompressionLevel.Optimal, true))
                {
                    CreateFileMetadataStream(package).CopyTo(compressor);
                }

                newEntry.FileListLength = filesMetadata.Length;

                uploadStream.Seek(0, SeekOrigin.End);
                filesMetadata.Seek(0, SeekOrigin.Begin);
                filesMetadata.CopyTo(uploadStream);
                uploadStream.SetLength(RoundToPageSize(uploadStream.Length));
            }

            uploadStream.Seek(0, SeekOrigin.Begin);

            lock (this.MetadataBlobLock)
            {
                newEntry.MetadataOffset = this.NextAvailableOffset;
                newEntry.FileListOffset = this.NextAvailableOffset + filesMetadataRelativeOffset;

                uploadStream.CopyTo(this.UploadCache);

                this.NextAvailableOffset += uploadStream.Length;

                if (this.UploadCache.Position > UploadCacheSize)
                {
                    this.UploadCache.SetLength(this.UploadCache.Position);
                    this.UploadCache.Seek(0, SeekOrigin.Begin);

                    this.UploadMetadata(this.UploadCache, this.UploadCacheOffset);

                    this.UploadCache.Seek(0, SeekOrigin.Begin);

                    this.UploadCacheOffset = this.NextAvailableOffset;
                }
            }

            return newEntry;
        }

        private void UploadMetadata(MemoryStream metadataBuffer, long offset)
        {
            var targetBlob = this.GetPageBlobClient();
            lock (this.MetadataBlobLock)
            {
                long requiredLength = offset + metadataBuffer.Length;
                if (requiredLength > this.PageBlobSize)
                {
                    this.PageBlobSize = Math.Max(RoundToPageSize(requiredLength), this.PageBlobSize + InitialBlobSize);
                    targetBlob.Resize(this.PageBlobSize);
                }
            }

            metadataBuffer.Seek(0, SeekOrigin.Begin);
            var uploadBuffer = new byte[4 * 1024 * 1024];
            int readCount;
            long pageBlobOffset = offset;

            do
            {
                readCount = metadataBuffer.Read(uploadBuffer, 0, uploadBuffer.Length);
                if (readCount > 0)
                {
                    using var pageStream = new MemoryStream(uploadBuffer, 0, readCount);
                    targetBlob.UploadPages(pageStream, pageBlobOffset);
                    pageBlobOffset += readCount;
                }
            } while (readCount > 0);

            this.BackBufferReadyEvent.Set();
        }

        public void Flush()
        {
            if (this.UploadCache.Position > 0)
            {
                this.UploadCache.SetLength(this.UploadCache.Position);
                this.UploadCache.Seek(0, SeekOrigin.Begin);
                this.UploadMetadata(this.UploadCache, this.UploadCacheOffset);
            }
            else
            {
                this.BackBufferReadyEvent.WaitOne();
            }
        }
    }
}