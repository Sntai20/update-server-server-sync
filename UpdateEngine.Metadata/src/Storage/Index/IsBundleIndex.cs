// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using UpdateEngine.Metadata.Storage.Index;
using System.Collections.Generic;
using System.Linq;

namespace UpdateEngine.Metadata.Index
{
    class IsBundleIndex : SimpleIndex<int, List<MicrosoftUpdatePackageIdentity>>, ISimpleMetadataIndex<int, List<MicrosoftUpdatePackageIdentity>>
    {
        public const string Name = AvailableIndexes.IsBundleIndexName;

        public override IndexDefinition Definition => MicrosoftUpdatePartitionRegistration.IsBundle;

        public IsBundleIndex(IIndexContainer container) : base(container, Name, MicrosoftUpdatePartitionRegistration.MicrosoftUpdatePartitionName)
        {
        }

        public override void IndexPackage(IPackage package, int packageIndex)
        {
            if (package is SoftwareUpdate softwareUpdate)
            {
                if (softwareUpdate.BundledUpdates != null && softwareUpdate.BundledUpdates.Count > 0)
                {
                    base.Add(packageIndex, softwareUpdate.BundledUpdates.ToList());
                }
            }
        }
    }
}
