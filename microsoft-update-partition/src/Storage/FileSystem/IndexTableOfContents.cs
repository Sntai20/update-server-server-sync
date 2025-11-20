// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PackageGraph.Storage.Index;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PackageGraph.Storage.Local
{
    class IndexTableOfContents
    {
        public int Version;

        public List<IndexDefinition> ContainedIndexes;

        [JsonIgnore]
        public const int CurrentVersion = 0;
    }
}
