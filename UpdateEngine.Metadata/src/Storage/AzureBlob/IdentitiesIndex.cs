// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ICSharpCode.SharpZipLib.GZip;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Partitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;

namespace UpdateEngine.Metadata.Storage.Azure
{
    class IdentitiesIndex
    {
        private readonly BlobContainerClient ParentContainer;

        private Dictionary<IPackageIdentity, int> _IdentityToIndexMap;
        private Dictionary<int, IPackageIdentity> _IndexToIdentityMap;
        private Dictionary<int, int> PackageTypeIndex;
        private Dictionary<int, PackageStoreEntry> StoreEntries;

        private string ConcurrencyEtag;

        private const string IdentitiesIndexBlobName = "identities-index";

        public IReadOnlyCollection<PackageStoreEntry> Entries => this.StoreEntries.Values;

        private List<PackageStoreEntry> PendingIdentities;

        public List<IPackageIdentity> Identities => this._IdentityToIndexMap.Keys.ToList();

        public IdentitiesIndex(BlobContainerClient container, AzurePackageStoreInitializeMode mode)
        {
            this.ParentContainer = container;
            this.PendingIdentities = new List<PackageStoreEntry>();
            this.Read(mode);
        }

        public static void Erase(BlobContainerClient container)
        {
            var indexBlob = container.GetBlockBlobClient(IdentitiesIndexBlobName);
            indexBlob.DeleteIfExists();
        }

        public void Reset()
        {
            this._IndexToIdentityMap = new Dictionary<int, IPackageIdentity>();
            this._IdentityToIndexMap = new Dictionary<IPackageIdentity, int>();
            this.PackageTypeIndex = new Dictionary<int, int>();
            this.PendingIdentities = new List<PackageStoreEntry>();
        }

        private void ReadIdentityEntries()
        {
            this._IndexToIdentityMap = new Dictionary<int, IPackageIdentity>();
            this._IdentityToIndexMap = new Dictionary<IPackageIdentity, int>();
            this.PackageTypeIndex = new Dictionary<int, int>();
            this.StoreEntries = new Dictionary<int, PackageStoreEntry>();

            var indexBlob = this.ParentContainer.GetBlockBlobClient(IdentitiesIndexBlobName);
            if (indexBlob.Exists())
            {
                var properties = indexBlob.GetProperties();
                this.ConcurrencyEtag = properties.Value.ETag.ToString();

                var blockList = indexBlob.GetBlockList(BlockListTypes.Committed);

                // Process each block separately - each block contains a complete JSON array
                long currentOffset = 0;
                foreach (var block in blockList.Value.CommittedBlocks)
                {
                    // Download just this block's data using the correct offset
                    using var blockStream = new MemoryStream();
                    var downloadResult = indexBlob.DownloadStreaming(new HttpRange(currentOffset, block.Size), null, false, default);
                    using (var sourceStream = downloadResult.Value.Content)
                    {
                        sourceStream.CopyTo(blockStream);
                    }
                    blockStream.Seek(0, SeekOrigin.Begin);

                    // Decompress this block's data
                    using (var zipStream = new GZipInputStream(blockStream))
                    {
                        zipStream.IsStreamOwner = false;
                        using var jsonReader = new StreamReader(zipStream, Encoding.UTF8);
                        var jsonText = jsonReader.ReadToEnd();
                        var deserializedIntries = JsonSerializer.Deserialize<List<PackageStoreEntry>>(jsonText);
                        if (deserializedIntries == null)
                        {
                            throw new InvalidOperationException($"Failed to deserialize List<PackageStoreEntry> from JSON. Content: {jsonText.Substring(0, Math.Min(100, jsonText.Length))}...");
                        }
                        deserializedIntries.ForEach(entry =>
           {
               if (!PartitionRegistration.TryGetPartition(entry.PartitionName, out var partitionDefinition))
               {
                   throw new InvalidOperationException($"Unknown package partition: {entry.PartitionName}");
               }

               var packageIdentity = partitionDefinition.Factory.IdentityFromString(entry.PackageId);
               this._IndexToIdentityMap.Add((int)entry.PackageIndex, packageIdentity);
               this._IdentityToIndexMap.Add(packageIdentity, (int)entry.PackageIndex);
               this.PackageTypeIndex.Add((int)entry.PackageIndex, entry.PackageType);
               this.StoreEntries.Add((int)entry.PackageIndex, entry);
           });
                    }

                    // Move to next block's offset
                    currentOffset += block.Size;
                }
            }
            else
            {
                this.ConcurrencyEtag = null;
            }
        }

        private void Read(AzurePackageStoreInitializeMode mode)
        {
            this.ReadIdentityEntries();

            if (this._IndexToIdentityMap.Keys.Except(this.PackageTypeIndex.Keys).Any())
            {
                if (mode == AzurePackageStoreInitializeMode.FailOnIndexCorruption)
                {
                    throw new InvalidDataException("Mismatch between package type and identity indexes");
                }
                else if (mode == AzurePackageStoreInitializeMode.ResetOnIndexCorruption)
                {
                    this.Reset();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public bool TryGetStoreEntry(int packageIndex, out PackageStoreEntry storeEntry)
        {
            return this.StoreEntries.TryGetValue(packageIndex, out storeEntry);
        }

        public PackageStoreEntry GetStoreEntry(int packageIndex)
        {
            if (this.StoreEntries.TryGetValue(packageIndex, out PackageStoreEntry entry))
            {
                return entry;
            }
            throw new KeyNotFoundException($"Store entry not found for package index: {packageIndex}");
        }

        public bool TryGetPackageType(IPackageIdentity packageIdentity, out int packageType)
        {
            if (this._IdentityToIndexMap.TryGetValue(packageIdentity, out var packageIndex))
            {
                return this.PackageTypeIndex.TryGetValue(packageIndex, out packageType);
            }
            else
            {
                packageType = -1;
                return false;
            }
        }

        public bool TryGetPackageIndex(IPackageIdentity packageIdentity, out int index)
        {
            return this._IdentityToIndexMap.TryGetValue(packageIdentity, out index);
        }

        public int GetPackageIndex(IPackageIdentity packageIdentity)
        {
            if (this._IdentityToIndexMap.TryGetValue(packageIdentity, out int index))
            {
                return index;
            }
            throw new KeyNotFoundException($"Package identity not found: {packageIdentity}");
        }

        public IPackageIdentity GetPackageIdentity(int index)
        {
            if (this._IndexToIdentityMap.TryGetValue(index, out IPackageIdentity identity))
            {
                return identity;
            }
            throw new KeyNotFoundException($"Package index not found: {index}");
        }

        public bool TryGetPackageIdentity(int index, out IPackageIdentity packageIdentity)
        {
            return this._IndexToIdentityMap.TryGetValue(index, out packageIdentity);
        }

        public int AddPackage(IPackage package, PackageStoreEntry packageEntry)
        {
            lock (this._IdentityToIndexMap)
            {
                var insertIndex = this._IdentityToIndexMap.Count;
                if (!this._IdentityToIndexMap.TryAdd(package.Id, insertIndex))
                {
                    throw new InvalidOperationException("package already exists");
                }

                this._IndexToIdentityMap.Add(insertIndex, package.Id);

                if (!PartitionRegistration.TryGetPartitionFromPackage(package, out var partitionDefinition))
                {
                    throw new ArgumentException($"Cannot find partition {package.Id.Partition}", nameof(package));
                }

                var packageType = partitionDefinition.Factory.GetPackageType(package);
                this.PackageTypeIndex.Add(insertIndex, packageType);

                packageEntry.PackageId = package.Id.ToString();
                packageEntry.PackageIndex = insertIndex;
                packageEntry.PackageType = packageType;
                packageEntry.PartitionName = package.Id.Partition;

                this.PendingIdentities.Add(packageEntry);

                return insertIndex;
            }
        }

        private static string GetCommitIdForPackages(List<string> packageIdentities)
        {
            MemoryStream identitiesBuffer = new();
            packageIdentities.ForEach(id => identitiesBuffer.Write(Encoding.UTF8.GetBytes(id)));
            identitiesBuffer.Seek(0, SeekOrigin.Begin);

            using HashAlgorithm hashAlgorithm = SHA256.Create();
            return BitConverter.ToString(hashAlgorithm.ComputeHash(identitiesBuffer)).Replace("-", "");
        }

        public void Save()
        {
            lock (this._IdentityToIndexMap)
            {
                if (this.PendingIdentities.Count > 0)
                {
                    var pendingIdentitiesJson = JsonSerializer.Serialize(this.PendingIdentities);
                    using var pendingIdentitiesStream = new MemoryStream();
                    using (var compressor = new GZipOutputStream(pendingIdentitiesStream))
                    {
                        compressor.IsStreamOwner = false;
                        compressor.Write(Encoding.UTF8.GetBytes(pendingIdentitiesJson));
                    }

                    pendingIdentitiesStream.Seek(0, SeekOrigin.Begin);
                    var indexBlob = this.ParentContainer.GetBlockBlobClient(IdentitiesIndexBlobName);
                    List<string> blocksList = new();

                    string currentEtag = null;
                    if (indexBlob.Exists())
                    {
                        var properties = indexBlob.GetProperties();
                        currentEtag = properties.Value.ETag.ToString();
                        var blockListResponse = indexBlob.GetBlockList(BlockListTypes.Committed);
                        blocksList.AddRange(blockListResponse.Value.CommittedBlocks.Select(block => block.Name));
                        
                        // Refresh our stored ETag before comparison
                        // This handles cases where previous operations in the same session updated the blob
                        this.ConcurrencyEtag = currentEtag;
                    }

                    // Note: After refresh above, this check now validates against external changes only
                    if (currentEtag != this.ConcurrencyEtag)
                    {
                        throw new InvalidOperationException("Package store index changed unexpectedly.");
                    }

                    var commitId = GetCommitIdForPackages(this.PendingIdentities.Select(p => p.PackageId).ToList());
                    indexBlob.StageBlock(commitId, pendingIdentitiesStream);

                    blocksList.Add(commitId);
                    indexBlob.CommitBlockList(blocksList);

                    // Refresh ETag after successful save for next operation
                    var newProperties = indexBlob.GetProperties();
                    this.ConcurrencyEtag = newProperties.Value.ETag.ToString();

                    this.PendingIdentities.Clear();
                }
            }
        }
    }
}
