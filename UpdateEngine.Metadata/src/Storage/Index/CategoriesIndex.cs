// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Metadata.Prerequisites;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using UpdateEngine.Metadata.Storage.Index;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UpdateEngine.Metadata.Index
{
    class CategoriesIndex : SimpleIndex<int, List<Guid>>, ISimpleMetadataIndex<int , List<Guid>>
    {
        public const string Name = AvailableIndexes.CategoriesIndexName;

        public override IndexDefinition Definition => MicrosoftUpdatePartitionRegistration.Categories;

        public CategoriesIndex(IIndexContainer container) : base(container, Name, MicrosoftUpdatePartitionRegistration.MicrosoftUpdatePartitionName)
        {
        }

        public override void IndexPackage(IPackage package, int packageIndex)
        {
            if (package is MicrosoftUpdatePackage microsoftUpdate && 
                microsoftUpdate.Prerequisites != null)
            {
                var categoryGuids = new List<Guid>();
                foreach (var prereq in microsoftUpdate.Prerequisites.OfType<AtLeastOne>().Where(p => p.IsCategory))
                {
                    categoryGuids.AddRange(prereq.Simple.Select(s => s.UpdateId));
                }

                if (categoryGuids.Count > 0)
                {
                    base.Add(packageIndex, categoryGuids);
                }
            }
        }
        public new bool TryGet(int packageIndex, out List<Guid> entry)
        {
            return base.TryGet(packageIndex, out entry);
        }
    }
}
