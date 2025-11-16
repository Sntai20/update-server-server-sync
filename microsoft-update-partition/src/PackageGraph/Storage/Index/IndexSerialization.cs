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
            
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException($"Empty or whitespace JSON content when deserializing type {typeof(T).Name}");
            }
            
            var result = JsonSerializer.Deserialize<T>(json);
            if (result == null)
            {
                throw new InvalidOperationException($"JsonSerializer.Deserialize returned null for type {typeof(T).Name}. JSON content: {json.Substring(0, Math.Min(100, json.Length))}...");
            }
            
            return result;
        }

        public static void SerializeIndexToStream<T>(Stream destinationStream, T index)
        {
            using var sw = new StreamWriter(destinationStream, Encoding.UTF8, 4 * 1024, true);
            var json = JsonSerializer.Serialize(index);
            sw.Write(json);
        }
    }
}
