// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Metadata.Metadata.Drivers;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;

namespace UpdateEngine.Metadata.Metadata.Parsers
{
    class DriverMetadataParser
    {
        public static List<DriverMetadata> GetAllMetadataEntries(XPathNavigator metadataNavigator, XmlNamespaceManager namespaceManager)
        {
            var returnList = new List<DriverMetadata>();

            XPathExpression driverMetadataQuery = metadataNavigator.Compile("upd:Update/upd:ApplicabilityRules/upd:Metadata/drv:WindowsDriverMetaData");
            driverMetadataQuery.SetContext(namespaceManager);

            var result = metadataNavigator.Evaluate(driverMetadataQuery) as XPathNodeIterator;

            if (result.Count > 0)
            {
                while (result.MoveNext())
                {
                    returnList.Add(new DriverMetadata(result.Current, namespaceManager));
                }
            }

            return returnList;
        }
    }
}
