// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Storage.Index;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UpdateEngine.Metadata.Storage.Local
{
    class IndexTableOfContents
    {
        public int Version;

        public List<IndexDefinition> ContainedIndexes;

        [JsonIgnore]
        public const int CurrentVersion = 0;
    }
}
