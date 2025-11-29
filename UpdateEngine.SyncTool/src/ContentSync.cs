// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.SyncTool
{
    using Azure.Storage.Blobs;
    using UpdateEngine.Metadata.Metadata;
    using UpdateEngine.Metadata.ObjectModel;
    using UpdateEngine.Metadata.Storage;
    using UpdateEngine.Metadata.Storage.Local;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    class ContentSync
    {
        public static void SyncContent(ContentSyncOptions options)
        {
            var metadataSource = MetadataStoreCreator.OpenFromOptions(options as IMetadataStoreOptions);
            if (metadataSource == null)
            {
                return;
            }

            var contentStore = GetContentStoreFromOptions(options);
            if (contentStore == null)
            {
                return;
            }

            var filter = FilterBuilder.MicrosoftUpdateFilterFromCommandLine(options as IMetadataFilterOptions);
            if (filter == null)
            {
                return;
            }

            var filteredPackages = filter.Apply(metadataSource);

            var filesToDownload = filteredPackages.Where(p => p.Files != null).SelectMany(p => p.Files).ToList();

            foreach(var microsoftUpdatePackage in filteredPackages.OfType<MicrosoftUpdatePackage>())
            {
                filesToDownload.AddRange(GetAllUpdateFiles(metadataSource, microsoftUpdatePackage));
            }

            filesToDownload = filesToDownload.Distinct().ToList();

            Console.WriteLine($"Sync {filesToDownload.Count} files, {filesToDownload.Sum(f => (long)f.Size)} bytes. Continue? (y/n)");
            if (Console.ReadKey().Key != ConsoleKey.Y)
            {
                return;
            }

            CancellationTokenSource cancelTokenSource = new();
            contentStore.Progress += ContentStore_Progress;
            contentStore.Download(filesToDownload, cancelTokenSource.Token);
        }

        /// <summary>
        /// Gets all files for an update, including files in bundled updates (recursive)
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        private static List<IContentFile> GetAllUpdateFiles(IMetadataStore metadataSource, MicrosoftUpdatePackage update)
        {
            var filesList = new List<IContentFile>();
            if (update.Files != null)
            {
                filesList.AddRange(update.Files);
            }

            if (update is SoftwareUpdate softwareUpdate && softwareUpdate.BundledUpdates != null)
            {
                foreach (var bundledUpdate in softwareUpdate.BundledUpdates)
                {
                    filesList.AddRange(
                        GetAllUpdateFiles(
                            metadataSource,
                            metadataSource.GetPackage(bundledUpdate) as MicrosoftUpdatePackage));
                }
            }

            return filesList;
        }

        static string ContentSyncLastFileDigest = "";

        private static void UpdateConsoleForMessageRefresh()
        {
            if (!Console.IsOutputRedirected)
            {
                Console.CursorLeft = 0;
            }
            else
            {
                Console.WriteLine();
            }
        }

        private static void ContentStore_Progress(object sender, UpdateEngine.Metadata.ObjectModel.ContentOperationProgress e)
        {
            if (e.File.Digest.DigestBase64 != ContentSyncLastFileDigest)
            {
                Console.WriteLine();
                ContentSyncLastFileDigest = e.File.Digest.DigestBase64;
            }

            switch(e.CurrentOperation)
            {
                case UpdateEngine.Metadata.ObjectModel.PackagesOperationType.DownloadFileProgress:
                    UpdateConsoleForMessageRefresh();
                    Console.Write("Sync'ing update content [{0}]: {1:000.00}%", e.Maximum, e.PercentDone);
                    break;
            }
        }

        private static IContentStore GetContentStoreFromOptions(ContentSyncOptions options)
        {
            switch(options.ContentStoreType)
            {
                case "local":
                    return new FileSystemContentStore(options.ContentPath);

                case "azure":
                    if (string.IsNullOrEmpty(options.ContentStoreConnectionString))
                    {
                        ConsoleOutput.WriteRed("Connection string required for Azure stores");
                        return null;
                    }

                    try 
                    {
                        var blobServiceClient = new BlobServiceClient(options.ContentStoreConnectionString);
                        
                        // Create console logger for BlobContentStore
                        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                        var logger = loggerFactory.CreateLogger<UpdateEngine.Metadata.Storage.Azure.BlobContentStore>();
                        
                        return UpdateEngine.Metadata.Storage.Azure.BlobContentStore.OpenOrCreate(
                            blobServiceClient, 
                            options.ContentPath, 
                            pathPrefix: "content",
                            logger: logger);
                    }
                    catch (Exception ex)
                    {
                        ConsoleOutput.WriteRed($"Invalid connection string: {ex.Message}");
                        return null;
                    }

                default:
                    ConsoleOutput.WriteRed("Content store type not supported.");
                    return null;

            }
        }
    }
}
