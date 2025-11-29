// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Storage.Index;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UpdateEngine.Metadata.Storage.Azure
{
    class IndexTableOfContents
    {
        public int Version;

        public List<IndexDefinition> ContainedIndexes = new();

        public List<int> IndexedPackages = new();

        [JsonIgnore]
        public const int CurrentVersion = 0;
    }
}
