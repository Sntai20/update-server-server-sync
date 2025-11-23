// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UpdateEngine.Metadata.Storage.Local
{
    class TableOfContent
    {
        public int TocVersion;
        public int DeltaSectionCount;
        public List<long> DeltaSectionPackageCount;

        [JsonIgnore]
        public const int CurrentVersion = 0;
    }
}
