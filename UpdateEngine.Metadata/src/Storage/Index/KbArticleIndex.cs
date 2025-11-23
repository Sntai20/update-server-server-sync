// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using UpdateEngine.Metadata.Storage.Index;

namespace UpdateEngine.Metadata.Index
{
    class KbArticleIndex : SimpleIndex<int, string>, ISimpleMetadataIndex<int, string>
    {
        public const string Name = AvailableIndexes.KbArticleIndexName;

        public override IndexDefinition Definition => MicrosoftUpdatePartitionRegistration.KbArticle;

        public KbArticleIndex(IIndexContainer container) : base(container, Name, MicrosoftUpdatePartitionRegistration.MicrosoftUpdatePartitionName)
        {
        }

        public override void IndexPackage(IPackage package, int packageIndex)
        {
            if (package is SoftwareUpdate softwareUpdate)
            {
                if (!string.IsNullOrEmpty(softwareUpdate.KBArticleId))
                {
                    base.Add(packageIndex, softwareUpdate.KBArticleId);
                }
            }
        }
    }
}
