// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using ICSharpCode.SharpZipLib.Zip;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Partitions;
using UpdateEngine.Metadata.Storage.Index;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace UpdateEngine.Metadata.Storage.Azure
{
    class IndexContainer : IIndexStreamContainer
    {
        Dictionary<string, IIndex> Indexes;
        List<IndexDefinition> UnknownIndexes;
        List<IndexDefinition> MissingIndexes;

        private const string IndexVirtualDirectoryName = "index";
        private const string TocBlobName = IndexVirtualDirectoryName + "/toc.json";

        public bool IsDirty = false;

        public bool ReIndexingRequired => this.MissingIndexes.Count > 0;

        IndexTableOfContents TOC;

        public enum IndexContainerStatus
        {
            Valid,
            Corrupt,
            MissingToc,
            BadTocVersion,
            UnknownIndexes,
            BadIndexVersion,
            MissingIndexes,
        }

        readonly BlobContainerClient ParentContainer;

        public IndexContainer(BlobContainerClient container)
        {
            this.ParentContainer = container;
            this.ReadTableOfContents();
        }

        public void ResetIndex()
        {
            this.CreateTableOfContents();
            this.CreateAllKnownIndexes();
        }

        public static void Erase(BlobContainerClient container)
        {
            var registeredIndexes = GetRegisteredIndexes();
            foreach (var registeredIndex in registeredIndexes)
            {
                var indexBlob = container.GetBlockBlobClient(GetIndexBlobNameFromDefinition(registeredIndex));
                indexBlob.DeleteIfExists();
            }

            var tocBlob = container.GetBlockBlobClient(TocBlobName);
            tocBlob.DeleteIfExists();
        }

        private void CreateAllKnownIndexes()
        {
            foreach (var partition in PartitionRegistration.GetAllPartitions())
            {
                foreach (var knownIndex in partition.Indexes)
                {
                    this.Indexes.Add(knownIndex.Name, knownIndex.Factory.CreateIndex(knownIndex, this));
                }
            }
        }

        private static string GetIndexBlobNameFromDefinition(IndexDefinition definition)
        {
            string indexEntry = IndexVirtualDirectoryName + "/";
            if (!string.IsNullOrEmpty(definition.PartitionName))
            {
                indexEntry += definition.PartitionName + "/";
            }

            indexEntry += definition.Name;

            return indexEntry;
        }

        public void Save()
        {
            bool indexesChanged = false;
            foreach (var index in this.Indexes.Values)
            {
                if (index.IsDirty)
                {
                    var indexBlob = this.ParentContainer.GetBlockBlobClient(GetIndexBlobNameFromDefinition(index.Definition));
                    using var indexStream = indexBlob.OpenWrite(overwrite: true);
                    using var compressor = new GZipStream(indexStream, CompressionLevel.Optimal, true);
                    index.Save(compressor);
                    indexesChanged = true;
                }
            }

            if (indexesChanged || this.IsDirty)
            {
                this.TOC.ContainedIndexes = this.Indexes.Select(index => index.Value.Definition).ToList();

                var tocBlob = this.ParentContainer.GetBlockBlobClient(TocBlobName);
                using var tocStream = tocBlob.OpenWrite(overwrite: true);
                using var tocWriter = new StreamWriter(tocStream);
                var tocJson = JsonSerializer.Serialize(this.TOC);
                tocWriter.Write(tocJson);
                tocWriter.Flush();
            }
        }

        private void CreateTableOfContents()
        {
            this.Indexes = new Dictionary<string, IIndex>();
            this.UnknownIndexes = new List<IndexDefinition>();
            this.MissingIndexes = new List<IndexDefinition>();
            this.TOC = new IndexTableOfContents
            {
                Version = IndexTableOfContents.CurrentVersion,
                IndexedPackages = new List<int>()
            };
        }

        public List<int> GetListOfMetadataIndexedPackages()
        {
            return this.TOC.IndexedPackages;
        }

        private void ReadTableOfContents()
        {
            var tocBlob = this.ParentContainer.GetBlockBlobClient(TocBlobName);
            if (!tocBlob.Exists())
            {
                this.ResetIndex();
                return;
            }

            using var blobReadStream = tocBlob.OpenRead();
            IndexTableOfContents toc;

            try
            {
                using var tocReader = new StreamReader(blobReadStream);
                var tocJson = tocReader.ReadToEnd();
                toc = JsonSerializer.Deserialize<IndexTableOfContents>(tocJson);
                if (toc == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize IndexTableOfContents from JSON. Content: {tocJson.Substring(0, Math.Min(100, tocJson.Length))}...");
                }
                if (toc.Version != IndexTableOfContents.CurrentVersion)
                {
                    toc = null;
                }
            }
            catch (JsonException jsonEx)
            {
                // JSON format error - likely due to Newtonsoft.Json vs System.Text.Json format differences
                throw new JsonException($"JSON deserialization failed in IndexContainer: {jsonEx.Message}", jsonEx);
            }
            catch (InvalidOperationException ioEx)
            {
                // Null deserialization results
                throw new InvalidOperationException($"Null result error in IndexContainer: {ioEx.Message}", ioEx);
            }
            catch (Exception ex)
            {
                // Other errors
                throw new InvalidDataException($"Unexpected error reading TOC in IndexContainer: {ex.Message}", ex);
            }

            if (toc != null)
            {
                var registeredIndexes = GetRegisteredIndexes();

                // Ensure ContainedIndexes is not null (handle empty or malformed JSON)
                if (toc.ContainedIndexes == null)
                {
                    toc.ContainedIndexes = new List<IndexDefinition>();
                }

                this.UnknownIndexes = toc
                    .ContainedIndexes
                    .Where(index => registeredIndexes.Any(knownIndex => knownIndex == index))
                    .ToList();

                toc.ContainedIndexes.RemoveAll(index => !registeredIndexes.Contains(index));

                this.MissingIndexes = registeredIndexes.Where(index => !toc.ContainedIndexes.Contains(index)).ToList();

                this.Indexes = new Dictionary<string, IIndex>();
                foreach (var index in toc.ContainedIndexes)
                {
                    var registeredIndex = registeredIndexes.Find(ri => ri.Equals(index));
                    this.Indexes.Add(index.Name, registeredIndex.Factory.CreateIndex(index, this));
                }

                foreach (var missingIndex in this.MissingIndexes)
                {
                    this.Indexes.Add(missingIndex.Name, missingIndex.Factory.CreateIndex(missingIndex, this));
                }

                this.TOC = toc;
            }
            else
            {
                this.ResetIndex();
                this.IsDirty = true;
            }
        }

        public bool TryGetIndex(string name, out IIndex index)
        {
            return this.Indexes.TryGetValue(name, out index);
        }

        public bool TryGetIndexReadStream(IndexDefinition index, out Stream indexStream)
        {
            var indexBlob = this.ParentContainer.GetBlockBlobClient(GetIndexBlobNameFromDefinition(index));
            if (indexBlob.Exists())
            {
                indexStream = new GZipStream(indexBlob.OpenRead(), CompressionMode.Decompress);
                return true;
            }
            else
            {
                indexStream = null;
                return false;
            }
        }

        public IndexContainerStatus GetStatus()
        {
            if (this.UnknownIndexes.Count > 0)
            {
                return IndexContainerStatus.UnknownIndexes;
            }

            if (this.MissingIndexes.Count > 0)
            {
                return IndexContainerStatus.MissingIndexes;
            }

            return IndexContainerStatus.Valid;
        }

        public void IndexPackage(IPackage package, int packageIndex)
        {
            foreach (var index in this.Indexes.Values)
            {
                if (string.IsNullOrEmpty(index.Definition.PartitionName) ||
                    PartitionRegistration.TryGetPartition(index.Definition.PartitionName, out var _))
                {
                    index.IndexPackage(package, packageIndex);
                }
            }

            this.TOC.IndexedPackages.Add(packageIndex);
            this.IsDirty = true;
        }

        public bool TrySimpleKeyLookup<T>(int packageIndex, string indexName, out T value)
        {
            if (this.Indexes.TryGetValue(indexName, out IIndex index))
            {
                return (index as ISimpleMetadataIndex<int, T>).TryGet(packageIndex, out value);
            }
            else
            {
                value = default;
                return false;
            }
        }

        public bool TryPackageLookupByCustomKey<T>(T key, string indexName, out int packageIndex)
        {
            if (this.Indexes.TryGetValue(indexName, out IIndex index))
            {
                return (index as ISimpleMetadataIndex<T, int>).TryGet(key, out packageIndex);
            }
            else
            {
                packageIndex = -1;
                return false;
            }
        }

        public bool TryPackageListLookupByCustomKey<T>(T key, string indexName, out List<int> packageIndexs)
        {
            if (this.Indexes.TryGetValue(indexName, out IIndex index))
            {
                return (index as ISimpleMetadataIndex<T, List<int>>).TryGet(key, out packageIndexs);
            }
            else
            {
                packageIndexs = null;
                return false;
            }
        }

        public bool TryListKeyLookup<T>(int packageIndex, string indexName, out List<T> value)
        {
            if (this.Indexes.TryGetValue(indexName, out IIndex index))
            {
                return (index as ISimpleMetadataIndex<int, List<T>>).TryGet(packageIndex, out value);
            }
            else
            {
                value = null;
                return false;
            }
        }

        public List<IndexDefinition> GetLoadedIndexes()
        {
            return this.Indexes.Select(i => i.Value.Definition).ToList();
        }

        private static List<IndexDefinition> GetRegisteredIndexes()
        {
            return PartitionRegistration
                .GetAllPartitions()
                .SelectMany(partition => partition.Indexes)
                .Where(index => index.Tag.Equals("stream"))
                .ToList();
        }
    }
}
