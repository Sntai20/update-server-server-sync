// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ICSharpCode.SharpZipLib.GZip;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Partitions;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.PackageGraph.Storage.Azure
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

                using var indexStream = new MemoryStream();
                indexBlob.DownloadTo(indexStream);
                indexStream.Seek(0, SeekOrigin.Begin);

                var blockList = indexBlob.GetBlockList(BlockListTypes.Committed);
                long currentOffset = 0;

                foreach (var block in blockList.Value.CommittedBlocks)
                {
                    indexStream.Seek(currentOffset, SeekOrigin.Begin);

                    using (var zipStream = new GZipInputStream(indexStream))
                    {
                        zipStream.IsStreamOwner = false;
                        using var jsonReader = new StreamReader(zipStream, Encoding.UTF8);
                        var jsonText = jsonReader.ReadToEnd();
                        var deserializedIntries = JsonSerializer.Deserialize<List<PackageStoreEntry>>(jsonText);
                        deserializedIntries?.ForEach(entry =>
           {
               if (!PartitionRegistration.TryGetPartition(entry.PartitionName, out var partitionDefinition))
               {
                   throw new Exception("Unknown package partition");
               }

               var packageIdentity = partitionDefinition.Factory.IdentityFromString(entry.PackageId);
               this._IndexToIdentityMap.Add((int)entry.PackageIndex, packageIdentity);
               this._IdentityToIndexMap.Add(packageIdentity, (int)entry.PackageIndex);
               this.PackageTypeIndex.Add((int)entry.PackageIndex, entry.PackageType);
               this.StoreEntries.Add((int)entry.PackageIndex, entry);
           });
                    }

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
                    throw new Exception("Mismatch between package type and identity indexes");
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
            return this.StoreEntries[packageIndex];
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
            return this._IdentityToIndexMap[packageIdentity];
        }

        public IPackageIdentity GetPackageIdentity(int index)
        {
            return this._IndexToIdentityMap[index];
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
                    throw new Exception("package already exists");
                }

                this._IndexToIdentityMap.Add(insertIndex, package.Id);

                if (!PartitionRegistration.TryGetPartitionFromPackage(package, out var partitionDefinition))
                {
                    throw new Exception($"Cannot find partition {package.Id.Partition}");
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
                    }

                    if (currentEtag != this.ConcurrencyEtag)
                    {
                        throw new Exception("Package store index changed unexpectedly.");
                    }

                    var commitId = GetCommitIdForPackages(this.PendingIdentities.Select(p => p.PackageId).ToList());
                    indexBlob.StageBlock(commitId, pendingIdentitiesStream);

                    blocksList.Add(commitId);
                    indexBlob.CommitBlockList(blocksList);

                    this.PendingIdentities.Clear();
                }
            }
        }
    }
}
