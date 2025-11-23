// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Partitions;
using UpdateEngine.Metadata.Storage.Index;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace UpdateEngine.Metadata.Storage.Azure
{
    enum AzurePackageStoreInitializeMode
    {
        ResetOnIndexCorruption,
        FailOnIndexCorruption
    }

    class ContainerPackageStore : IMetadataStore, IMetadataLookup
    {
        public event EventHandler<PackageStoreEventArgs> MetadataCopyProgress;
        public event EventHandler<PackageStoreEventArgs> PackageIndexingProgress;

#pragma warning disable 0067
        public event EventHandler<PackageStoreEventArgs> OpenProgress;
#pragma warning restore 0067

        public event EventHandler<PackageStoreEventArgs> PackagesAddProgress;

        readonly BlobContainerClient ParentContainer;

        readonly MetadataStore Metadata;

        const string MetadataBlobName = "metadata";
        const string TocBlobName = "toc";

        readonly IndexContainer IndexContainer;

        readonly IdentitiesIndex Identities;

        bool IsDisposed = false;

        readonly List<IPackage> PendingPackages = new();

        /// <inheritdoc cref="IMetadataStore.IsReindexingRequired"/>
        public bool IsReindexingRequired { get; private set; } = false;

        /// <inheritdoc cref="IMetadataStore.IsMetadataIndexingSupported"/>
        public bool IsMetadataIndexingSupported { get; private set; } = true;

        private ContainerPackageStore(BlobContainerClient container, AzurePackageStoreInitializeMode mode)
        {
            this.ParentContainer = container;

            this.Identities = new IdentitiesIndex(container, mode);
            this.IndexContainer = new IndexContainer(container);

            this.Metadata = new MetadataStore(this.ParentContainer);

            var indexedIdentities = this.Identities.Identities.Select(identity => identity.OpenIdHex).ToList();

            var metadataIndexedIdentities = this.IndexContainer.GetListOfMetadataIndexedPackages()
                .Where(index => this.Identities.TryGetPackageIdentity(index, out var identity))
                .Select(index => { this.Identities.TryGetPackageIdentity(index, out var identity); return identity.OpenIdHex; });
            var notMetadataIndexedIdentities = indexedIdentities.Except(metadataIndexedIdentities).ToList();
            if (notMetadataIndexedIdentities.Count > 0)
            {
                this.IsReindexingRequired = true;
            }

            this.IsReindexingRequired |= this.IndexContainer.ReIndexingRequired;

            var missingMetadata = indexedIdentities.Except(indexedIdentities).ToList();
            if (missingMetadata.Count > 0)
            {
                if (mode == AzurePackageStoreInitializeMode.FailOnIndexCorruption)
                {
                    throw new InvalidDataException($"The underlying metadata store does not contain all indexed packages from the store. Missing: {missingMetadata.Count}");
                }
                else if (mode == AzurePackageStoreInitializeMode.ResetOnIndexCorruption)
                {
                    this.Identities.Reset();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public static ContainerPackageStore OpenExisting(BlobContainerClient container)
        {
            return new ContainerPackageStore(container, AzurePackageStoreInitializeMode.FailOnIndexCorruption);
        }

        public static ContainerPackageStore OpenExisting(BlobServiceClient client, string containerName)
        {
            var container = client.GetBlobContainerClient(containerName);

            return new ContainerPackageStore(container, AzurePackageStoreInitializeMode.FailOnIndexCorruption);
        }

        public static void Erase(BlobServiceClient client, string containerName)
        {
            var containerRef = client.GetBlobContainerClient(containerName);
            if (containerRef.Exists())
            {
                var tocReference = containerRef.GetBlobClient(TocBlobName);
                tocReference.DeleteIfExists();

                for (int i = 0; i < int.MaxValue; i++)
                {
                    // In the new SDK, use the container's GetBlobClient URI string to create PageBlobClient
                    var blobUriString = containerRef.GetBlobClient(MetadataBlobName + i.ToString()).Uri.ToString();
                    var pageBlobClient = new PageBlobClient(blobUriString, containerRef.GetParentBlobServiceClient().GetProperties().Value.DefaultServiceVersion, null);

                    if (pageBlobClient.Exists())
                    {
                        pageBlobClient.Delete();
                    }
                    else
                    {
                        break;
                    }
                }

                IndexContainer.Erase(containerRef);
                IdentitiesIndex.Erase(containerRef);
            }
        }

        public static ContainerPackageStore OpenOrCreate(BlobServiceClient client, string containerName)
        {
            var container = client.GetBlobContainerClient(containerName);
            container.CreateIfNotExists();

            return new ContainerPackageStore(container, AzurePackageStoreInitializeMode.ResetOnIndexCorruption);
        }

        public static bool Exists(BlobServiceClient client, string containerName)
        {
            var container = client.GetBlobContainerClient(containerName);
            return container.Exists();
        }

        public bool ContainsMetadata(IPackageIdentity packageIdentity)
        {
            return this.Identities.TryGetPackageIndex(packageIdentity, out var _);
        }

        public Stream GetMetadata(IPackageIdentity packageIdentity)
        {
            if (this.Identities.TryGetPackageIndex(packageIdentity, out var packageIndex) &&
            this.Identities.TryGetStoreEntry(packageIndex, out var storeEntry))
            {
                return this.Metadata.GetMetadata(storeEntry);
            }
            else
            {
                throw new KeyNotFoundException($"Package {packageIdentity} not found");
            }
        }

        public void Dispose()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("package store");
            }

            this.Flush();
            this.IsDisposed = true;
        }

        public void AddPackage(IPackage package)
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("package store");
            }

            if (this.Identities.TryGetPackageIndex(package.Id, out var _))
            {
                return;
            }

            lock (this.Identities)
            {
                var entry = this.Metadata.AddPackage(package);

                var packageIndex = this.Identities.AddPackage(package, entry);
                this.IndexContainer.IndexPackage(package, packageIndex);

                this.PendingPackages.Add(package);
            }
        }

        public List<T> GetFiles<T>(IPackageIdentity packageIdentity)
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("package store");
            }

            if (this.Identities.TryGetPackageIndex(packageIdentity, out var packageIndex) &&
       this.Identities.TryGetStoreEntry(packageIndex, out var storeEntry))
            {
                return this.Metadata.GetFiles<T>(storeEntry);
            }
            else
            {
                throw new KeyNotFoundException($"Package {packageIdentity} not found");
            }
        }

        public void AddPackages(IEnumerable<IPackage> packages)
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("package store");
            }

            var progressArgs = new PackageStoreEventArgs() { Current = 0, Total = packages.Count() };
            this.PackagesAddProgress?.Invoke(this, progressArgs);
            foreach (var package in packages)
            {
                this.AddPackage(package);

                progressArgs.Current++;
                this.PackagesAddProgress?.Invoke(this, progressArgs);
            }
        }

        public void Flush()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("package store");
            }

            this.Identities.Save();
            this.IndexContainer.Save();
            this.Metadata.Flush();

            this.PendingPackages.Clear();
        }

        public IEnumerator<IPackage> GetEnumerator()
        {
            return new AzurePackageEnumerator(this.GetPackagesList(), this);
        }

        public List<IPackageIdentity> GetPackageIdentities()
        {
            return this.Identities.Identities.ToList();
        }

        public void CopyTo(IMetadataSink destination, CancellationToken cancelToken)
        {
            var identitiesToCopy = this.Identities.Identities.ToDictionary(id => id.ToString());
            var copyCount = identitiesToCopy.Count;

            HashSet<string> matchingPackagesInDestination = new();
            if (destination is IMetadataStore destinationPackageStore)
            {
                matchingPackagesInDestination = destinationPackageStore.GetPackageIdentities().Select(i => i.ToString()).ToHashSet();
            }

            copyCount -= matchingPackagesInDestination.Count;

            var progressArgs = new PackageStoreEventArgs() { Total = copyCount, Current = 0 };
            this.MetadataCopyProgress?.Invoke(copyCount, progressArgs);

            var sortedEntries = this.Identities.Entries.ToList().OrderBy(e => e.MetadataOffset);

            foreach (var entry in sortedEntries)
            {
                if (!matchingPackagesInDestination.Contains(entry.PackageId))
                {
                    var metadataStream = this.Metadata.GetMetadata(entry);

                    if (!PartitionRegistration.TryGetPartition(entry.PartitionName, out var partition))
                    {
                        throw new Exception($"There is no registered partition {entry.PartitionName}");
                    }

                    var package = partition.Factory.FromStream(metadataStream, this);
                    destination.AddPackage(package);

                    progressArgs.Current++;
                    this.MetadataCopyProgress?.Invoke(copyCount, progressArgs);
                }
            }
        }

        public bool ContainsPackage(IPackageIdentity packageIdentity)
        {
            return this.Identities.TryGetPackageIndex(packageIdentity, out var _);
        }

        public int GetPackageIndex(IPackageIdentity packageIdentity)
        {
            if (this.Identities.TryGetPackageIndex(packageIdentity, out var index))
            {
                return index;
            }
            else
            {
                return -1;
            }
        }

        public IPackage GetPackage(IPackageIdentity packageIdentity)
        {
            if (!this.Identities.TryGetPackageType(packageIdentity, out var packageType))
            {
                throw new InvalidOperationException($"Package type is not available for package {packageIdentity}");
            }

            if (!PartitionRegistration.TryGetPartition(packageIdentity.Partition, out var partition))
            {
                throw new Exception($"There is no registered partition {packageIdentity.Partition}");
            }

            return partition.Factory.FromStore(packageType, packageIdentity, this, this);
        }

        public IPackage GetPackage(int packageIndex)
        {
            if (this.Identities.TryGetPackageIdentity(packageIndex, out var packageIdentity))
            {
                return this.GetPackage(packageIdentity);
            }
            else
            {
                throw new Exception($"Index {packageIndex} not found");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private List<KeyValuePair<IPackageIdentity, PartitionDefinition>> GetPackagesList()
        {
            var packagePaths = new List<KeyValuePair<IPackageIdentity, PartitionDefinition>>();
            var allRegisteredPartitions = PartitionRegistration
                    .GetAllPartitions()
                   .Where(partition => partition.HandlesIdentities)
                    .ToDictionary(partition => partition.Name);

            foreach (var identity in this.Identities.Identities)
            {
                if (!allRegisteredPartitions.TryGetValue(identity.Partition, out var partitionDefinition))
                {
                    throw new Exception($"There is no registered partition {identity.Partition}");
                }

                packagePaths.Add(new KeyValuePair<IPackageIdentity, PartitionDefinition>(identity, partitionDefinition));
            }

            return packagePaths;
        }

        public bool TrySimpleKeyLookup<T>(IPackageIdentity packageIdentity, string indexName, out T value)
        {
            if (!this.Identities.TryGetPackageIndex(packageIdentity, out int packageIndex))
            {
                value = default(T);
                return false;
            }

            return this.IndexContainer.TrySimpleKeyLookup(packageIndex, indexName, out value);
        }

        public bool TryPackageLookupByCustomKey<T>(T key, string indexName, out IPackageIdentity value)
        {
            if (this.IndexContainer.TryPackageLookupByCustomKey(key, indexName, out int packageIndex))
            {
                return this.Identities.TryGetPackageIdentity(packageIndex, out value);
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TryPackageListLookupByCustomKey<T>(T key, string indexName, out List<IPackageIdentity> value)
        {
            if (this.IndexContainer.TryPackageListLookupByCustomKey(key, indexName, out List<int> packageIndex))
            {
                var identities = new List<IPackageIdentity>();
                foreach (var index in packageIndex)
                {
                    if (this.Identities.TryGetPackageIdentity(index, out var identity))
                    {
                        identities.Add(identity);
                    }
                }
                value = identities;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TryListKeyLookup<T>(IPackageIdentity packageIdentity, string indexName, out List<T> value)
        {
            if (!this.Identities.TryGetPackageIndex(packageIdentity, out int packageIndex))
            {
                value = null;
                return false;
            }

            return this.IndexContainer.TryListKeyLookup<T>(packageIndex, indexName, out value);
        }

        public List<IndexDefinition> GetAvailableIndexes()
        {
            return this.IndexContainer.GetLoadedIndexes();
        }

        private void CheckIndex(bool forceReindex)
        {
            lock (this.Identities)
            {
                if (!this.IsReindexingRequired && !forceReindex)
                {
                    return;
                }

                IndexContainer.Erase(this.ParentContainer);
                this.IndexContainer.ResetIndex();

                PackageStoreEventArgs progressEvent = new() { Total = this.Identities.Identities.Count, Current = 0 };

                for (int packageIndex = 0; packageIndex <= this.Identities.Entries.Max(e => e.PackageIndex); packageIndex++)
                {
                    if (!this.Identities.TryGetPackageIdentity(packageIndex, out var packageIdentity))
                        continue;
                    
                    var packageStream = this.Metadata.GetMetadata(this.Identities.GetStoreEntry(packageIndex));
                    if (PartitionRegistration.TryGetPartition(packageIdentity.Partition, out var partitionDefinition))
                    {
                        var parsedPackage = partitionDefinition.Factory.FromStream(packageStream, this);
                        if (this.Identities.TryGetPackageIndex(packageIdentity, out int validPackageIndex))
                        {
                            this.IndexContainer.IndexPackage(parsedPackage, validPackageIndex);
                        }
                    }
                    else
                    {
                        throw new Exception($"Partition not found {packageIdentity.Partition}");
                    }

                    progressEvent.Current++;
                    this.PackageIndexingProgress?.Invoke(this, progressEvent);
                }

                this.IsReindexingRequired = false;
            }
        }

        /// <inheritdoc cref="IMetadataStore.ReIndex"/>
        public void ReIndex()
        {
            this.CheckIndex(true);
        }

        public void CopyTo(IMetadataSink destination, IMetadataFilter filter, CancellationToken cancelToken)
        {
            var identitiesToCopy = filter.Apply(this).Select(p => p.Id).ToDictionary(i => i.ToString());
            var copyCount = identitiesToCopy.Count;

            HashSet<string> matchingPackagesInDestination = new();
            if (destination is IMetadataStore destinationPackageStore)
            {
                matchingPackagesInDestination = destinationPackageStore.GetPackageIdentities().Select(i => i.ToString()).Intersect(identitiesToCopy.Keys).ToHashSet();
            }

            copyCount -= matchingPackagesInDestination.Count;

            var progressArgs = new PackageStoreEventArgs() { Total = copyCount, Current = 0 };
            this.MetadataCopyProgress?.Invoke(copyCount, progressArgs);

            if (copyCount == 0)
            {
                return;
            }

            foreach (var entry in this.Identities.Entries)
            {
                if (identitiesToCopy.ContainsKey(entry.PackageId) && !matchingPackagesInDestination.Contains(entry.PackageId))
                {
                    var metadataStream = this.Metadata.GetMetadata(entry);

                    if (!PartitionRegistration.TryGetPartition(entry.PartitionName, out var partition))
                    {
                        throw new Exception($"There is no registered partition {entry.PartitionName}");
                    }

                    var package = partition.Factory.FromStream(metadataStream, this);
                    destination.AddPackage(package);

                    progressArgs.Current++;
                    this.MetadataCopyProgress?.Invoke(copyCount, progressArgs);
                }
            }
        }

        public IReadOnlyList<IPackage> GetPendingPackages()
        {
            return this.PendingPackages.AsReadOnly();
        }

        class AzurePackageEnumerator : IEnumerator<IPackage>
        {
            readonly ContainerPackageStore _Source;
            readonly IEnumerator<KeyValuePair<IPackageIdentity, PartitionDefinition>> IdentitiesEnumerator;

            public AzurePackageEnumerator(List<KeyValuePair<IPackageIdentity, PartitionDefinition>> paths, ContainerPackageStore metadataSource)
            {
                this._Source = metadataSource;
                this.IdentitiesEnumerator = paths.GetEnumerator();
            }

            public object Current => this.GetCurrent();

            IPackage IEnumerator<IPackage>.Current => this.GetCurrent();

            private IPackage GetCurrent()
            {
                return this._Source.GetPackage(this.IdentitiesEnumerator.Current.Key);
            }

            public void Dispose()
            {
                this.IdentitiesEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                return this.IdentitiesEnumerator.MoveNext();
            }

            public void Reset()
            {
                this.IdentitiesEnumerator.Reset();
            }
        }
    }
}