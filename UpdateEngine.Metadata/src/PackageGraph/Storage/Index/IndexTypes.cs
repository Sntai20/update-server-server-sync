// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using UpdateEngine.Metadata.Partitions;
using System;

namespace UpdateEngine.Metadata.Storage.Index
{
    abstract class AvailableIndexes
    {
        public const string TitlesIndexName = "titles";
    }

    class InternalIndexFactory : IIndexFactory
    {
        public IIndex CreateIndex(IndexDefinition definition, IIndexContainer container)
        {
            return definition.Name switch
            {
                AvailableIndexes.TitlesIndexName => new TitlesIndex(container),
                _ => throw new NotImplementedException(),
            };
        }
    }
}
