// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.ObjectModel;
using System.IO;

namespace UpdateEngine.Metadata.Storage.Index
{
    interface IIndex
    {
        void Save(Stream destination);

        bool IsDirty { get; }

        void IndexPackage(IPackage package, int packageIndex);

        IndexDefinition Definition { get; }
    }
}
