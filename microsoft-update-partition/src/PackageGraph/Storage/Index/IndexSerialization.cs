// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.IO;
using System.Text;

namespace Microsoft.PackageGraph.Storage.Index
{
    class IndexSerialization
    {
        public static T DeserializeIndexFromStream<T>(Stream inputStream)
        {
            using var sr = new StreamReader(inputStream);
            var json = sr.ReadToEnd();
            return JsonSerializer.Deserialize<T>(json)!;
        }

        public static void SerializeIndexToStream<T>(Stream destinationStream, T index)
        {
            using var sw = new StreamWriter(destinationStream, Encoding.UTF8, 4 * 1024, true);
            var json = JsonSerializer.Serialize(index);
            sw.Write(json);
        }
    }
}
