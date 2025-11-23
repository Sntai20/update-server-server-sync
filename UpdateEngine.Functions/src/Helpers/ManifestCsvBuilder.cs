namespace UpdateEngine.Helpers;

using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Storage;
using System.Text;

public static class ManifestCsvBuilder
{
    public static string GenerateDetailedManifestCsv(IMetadataStore metadataStore)
    {
        var filter = new MetadataFilter
        {
            TitleFilter = string.Empty,
            HardwareIdFilter = string.Empty,
            SkipSuperseded = false,
            FirstX = 0
        };

        var packages = filter.Apply(metadataStore).ToList();
        var csv = new StringBuilder();
        csv.AppendLine("LastWriteTime,LastSyncTime,FileName,FileHash,FileSize,Id,Title,Type,IsSuperseded,FilePath");
        var lastSyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        foreach (var package in packages)
        {
            var isSuperseded = false;
            var title = (package.Title ?? "").Replace("\"", "\"\"");
            var type = package.GetType().Name;
            var id = package.Id.OpenId;

            if (package is SoftwareUpdate softwareUpdate && softwareUpdate.Files != null)
            {
                isSuperseded = softwareUpdate.IsSupersededBy?.Any() == true;
                foreach (var file in softwareUpdate.Files)
                {
                    var fileName = file.FileName ?? string.Empty;
                    var fileHash = file.Digest?.HexString ?? string.Empty;
                    var fileSize = file.Size;
                    var lastWriteTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    var filePath = file.Source ?? string.Empty;
                    csv.AppendLine($"\"{lastWriteTime}\",\"{lastSyncTime}\",\"{fileName}\",\"{fileHash}\",{fileSize},\"{id}\",\"{title}\",\"{type}\",{isSuperseded.ToString().ToLower()},\"{filePath}\"");
                }
            }
        }
        return csv.ToString();
    }

    public static string GenerateSummaryManifestCsv(IMetadataStore metadataStore)
    {
        var filter = new MetadataFilter
        {
            TitleFilter = string.Empty,
            HardwareIdFilter = string.Empty,
            SkipSuperseded = false,
            FirstX = 0
        };
        var packages = filter.Apply(metadataStore).ToList();
        var csv = new StringBuilder();
        csv.AppendLine("Id,Title,Type,HasContent,IsSuperseded,FileSize,FileCount");
        foreach (var package in packages)
        {
            var hasContent = false;
            var fileSize = 0L;
            var fileCount = 0;
            var isSuperseded = false;
            if (package is SoftwareUpdate softwareUpdate)
            {
                hasContent = softwareUpdate.Files?.Any() == true;
                fileSize = softwareUpdate.Files?.Sum(f => (long)f.Size) ?? 0;
                fileCount = softwareUpdate.Files?.Count() ?? 0;
                isSuperseded = softwareUpdate.IsSupersededBy?.Any() == true;
            }
            var title = (package.Title ?? "").Replace("\"", "\"\"");
            var type = package.GetType().Name;
            csv.AppendLine($"\"{package.Id.OpenId}\",\"{title}\",\"{type}\",{hasContent},{isSuperseded},{fileSize},{fileCount}");
        }
        return csv.ToString();
    }
}
