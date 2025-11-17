// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Storage.Blobs;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace Microsoft.PackageGraph.Utilitites.Upsync
{
    public class MetadataStoreOptions : IMetadataStoreOptions
    {
        public string Alias { get; set; }
        public string Path { get; set; }

        public string Type { get; set; }

        public string StoreConnectionString { get; set; }
    }

    class MetadataStoreCreator
    {
        public static IMetadataStore CreateFromOptions(IMetadataStoreOptions sourceOptions)
            {
                if (!string.IsNullOrEmpty(sourceOptions.Alias))
                {
                    List<StoreAliasCreateOptions> storeAliases = LoadStoreAliases(StoreAliasesConfigFile);
                    var alias = sourceOptions.Alias;
                    sourceOptions = storeAliases.FirstOrDefault(a => a.Alias == sourceOptions.Alias);
                    if (sourceOptions == null)
                    {
                        Console.WriteLine($"Alias {alias} not found");
                        return null;
                    }
                }

                IMetadataStore source = null;
                if (!Console.IsOutputRedirected)
                {
                    Console.Write($"Creating package source [{sourceOptions.Path}] ");
                }

                if (sourceOptions.Type == "local")
                {
                    try
                    {
                        source = PackageStore.OpenOrCreate(sourceOptions.Path);
                        if (!Console.IsOutputRedirected)
                        {
                            ConsoleOutput.WriteGreen("Done!");
                        }

                        if (source.IsReindexingRequired)
                        {
                            ConsoleOutput.WriteRed("Warning: Package source must be reindexed!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                        ConsoleOutput.WriteRed($"Cannot create the package store: {ex.Message}");
                    }
                }
                else if (sourceOptions.Type == "azure-blob")
                {
                    string? containerName = null;
                    // If StoreConnectionString is empty, treat Path as a URI to the container
                    if (string.IsNullOrEmpty(sourceOptions.StoreConnectionString))
                    {
                        if (Uri.TryCreate(sourceOptions.Path, UriKind.Absolute, out var uri))
                        {
                            // Use last segment as container name
                            var segments = uri.Segments;
                            containerName = segments.Length > 0 ? segments[segments.Length - 1].Trim('/') : null;
                        }
                        else
                        {
                            containerName = sourceOptions.Path.Trim('/');
                        }
                        if (string.IsNullOrEmpty(containerName))
                        {
                            ConsoleOutput.WriteRed("Container name could not be determined from Path.");
                            return null;
                        }
                        var containerClient = new BlobContainerClient(new Uri(sourceOptions.Path));
                        return Microsoft.PackageGraph.Storage.Azure.PackageStore.Open(containerClient);
                    }
                    else
                    {
                        try
                        {
                            var blobServiceClient = new BlobServiceClient(sourceOptions.StoreConnectionString);
                            if (Uri.TryCreate(sourceOptions.Path, UriKind.Absolute, out var uri))
                            {
                                var segments = uri.Segments;
                                containerName = segments.Length > 0 ? segments[segments.Length - 1].Trim('/') : null;
                            }
                            else
                            {
                                containerName = sourceOptions.Path.Trim('/');
                            }
                            if (string.IsNullOrEmpty(containerName))
                            {
                                ConsoleOutput.WriteRed("Container name could not be determined from Path.");
                                return null;
                            }
                            return Microsoft.PackageGraph.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, containerName);
                        }
                        catch (ArgumentException ex)
                        {
                            ConsoleOutput.WriteRed($"The connection string is invalid (argument error). Error: {ex.Message}");
                            return null;
                        }
                        catch (FormatException ex)
                        {
                            ConsoleOutput.WriteRed($"The connection string is invalid (format error). Error: {ex.Message}");
                            return null;
                        }
                        catch (Azure.RequestFailedException ex)
                        {
                            ConsoleOutput.WriteRed($"Azure request failed. Error: {ex.Message}");
                            return null;
                        }
                        catch (Exception ex)
                        {
                            ConsoleOutput.WriteRed($"Invalid connection string: {ex.Message}");
                            return null;
                        }
                    }
                }
                else
                {
                    ConsoleOutput.WriteRed($"Unknown store type {sourceOptions.Type}");
                }

                return source;
                }
        private const string StoreAliasesConfigFile = "store-aliases.json";

        public static void CreateAlias(StoreAliasCreateOptions storeOptions)
        {
            List<StoreAliasCreateOptions> storeAliases = LoadStoreAliases(StoreAliasesConfigFile);

            storeAliases.RemoveAll(alias => alias.Alias == storeOptions.Alias);

            var store = CreateFromOptions(
                new MetadataStoreOptions()
                {
                    Path = storeOptions.Path,
                    StoreConnectionString = storeOptions.StoreConnectionString,
                    Type = storeOptions.Type
                });
            if (store != null)
            {
                storeAliases.Add(storeOptions);
                File.WriteAllText(StoreAliasesConfigFile, JsonSerializer.Serialize(storeAliases));
            }
        }

        public static void DeleteAlias(StoreAliasDeleteOptions options)
        {
            List<StoreAliasCreateOptions> storeAliases = LoadStoreAliases(StoreAliasesConfigFile);
            if (options.All)
            {
                File.Delete(StoreAliasesConfigFile);
                Console.WriteLine("All store aliases have been deleted!");
            }
            else
            {
                if (storeAliases.RemoveAll(alias => alias.Alias == options.Alias) > 0)
                {
                    File.WriteAllText(StoreAliasesConfigFile, JsonSerializer.Serialize(storeAliases));
                    Console.WriteLine($"Alias {options.Alias} deleted");
                }
                else
                {
                    Console.WriteLine($"Alias {options.Alias} not found");
                }
            }
        }

        public static void ListAliases(StoreAliasListOptions options)
        {
            List<StoreAliasCreateOptions> storeAliases = LoadStoreAliases(StoreAliasesConfigFile);
            var aliasesToList = 
                string.IsNullOrEmpty(options.Alias) ? 
                storeAliases : 
                storeAliases.Where(alias => alias.Alias == options.Alias);
            

            if (aliasesToList.Any())
            {
                foreach (var alias in storeAliases)
                {
                    Console.WriteLine($"Alias            : {alias.Alias}");
                    Console.WriteLine($"Path             : {alias.Path}");
                    Console.WriteLine($"Type             : {alias.Type}");
                    Console.WriteLine($"Connection string: {alias.StoreConnectionString}");
                }
            }
            else
            {
                Console.WriteLine($"No aliases found");
            }
        }

        private static List<StoreAliasCreateOptions> LoadStoreAliases(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<StoreAliasCreateOptions>>(File.ReadAllText(path));
                }
                catch (Exception) { }
            }

            return new List<StoreAliasCreateOptions>();
        }

        public static IMetadataStore OpenFromOptions(IMetadataStoreOptions sourceOptions)
        {
            if (!string.IsNullOrEmpty(sourceOptions.Alias))
            {
                List<StoreAliasCreateOptions> storeAliases = LoadStoreAliases(StoreAliasesConfigFile);
                var alias = sourceOptions.Alias;
                sourceOptions = storeAliases.FirstOrDefault(a => a.Alias == sourceOptions.Alias);
                if (sourceOptions == null)
                {
                    Console.WriteLine($"Alias {alias} not found");
                    return null;
                }
            }

            IMetadataStore source = null;
            if (!Console.IsOutputRedirected)
            {
                Console.Write($"Opening package source [{sourceOptions.Path}] ");
            }

            if (sourceOptions.Type == "local")
            {
                try
                {
                    source = PackageStore.Open(sourceOptions.Path);
                    if (!Console.IsOutputRedirected)
                    {
                        ConsoleOutput.WriteGreen("Done!");
                    }

                    if (source.IsReindexingRequired)
                    {
                        ConsoleOutput.WriteRed("Warning: Package source must be reindexed!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    ConsoleOutput.WriteRed($"Cannot open the package store: {ex.Message}");
                }
            }
            else if (sourceOptions.Type == "azure-blob")
            {
                if (string.IsNullOrEmpty(sourceOptions.StoreConnectionString))
                {
                    var containerClient = new BlobContainerClient(new Uri(sourceOptions.Path));
                    return Microsoft.PackageGraph.Storage.Azure.PackageStore.Open(containerClient);
                }
                else
                {
                    try
                    {
                        var blobServiceClient = new BlobServiceClient(sourceOptions.StoreConnectionString);
                        return Microsoft.PackageGraph.Storage.Azure.PackageStore.Open(blobServiceClient, sourceOptions.Path);
                    }
                    catch (ArgumentException ex)
                    {
                        ConsoleOutput.WriteRed($"The connection string is invalid (argument error). Error: {ex.Message}");
                        return null;
                    }
                    catch (FormatException ex)
                    {
                        ConsoleOutput.WriteRed($"The connection string is invalid (format error). Error: {ex.Message}");
                        return null;
                    }
                    catch (Azure.RequestFailedException ex)
                    {
                        ConsoleOutput.WriteRed($"Azure request failed. Error: {ex.Message}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        ConsoleOutput.WriteRed($"Invalid connection string: {ex.Message}");
                        return null;
                    }
                }
            }
            else
            {
                ConsoleOutput.WriteRed($"Unknown store type {sourceOptions.Type}");
            }

            return source;
        }
    }
}
