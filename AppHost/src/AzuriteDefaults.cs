namespace AppHost;

public class AzuriteDefaults
{
    public const string AccountName = "devstoreaccount1";
    public const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    public const int BlobPort = 10000;
    public const int QueuePort = 10001;
    public const int TablePort = 10002;

    public static string GetConnectionString() =>
        $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};" +
        $"BlobEndpoint=http://127.0.0.1:{BlobPort}/{AccountName};" +
        $"QueueEndpoint=http://127.0.0.1:{QueuePort}/{AccountName};" +
        $"TableEndpoint=http://127.0.0.1:{TablePort}/{AccountName};";
}