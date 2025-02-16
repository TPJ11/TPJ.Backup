using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace TPJ.Backup.Store.AzureBlobStorage;

public abstract class AzureBlobStorageBase(IConfiguration configuration)
{
    internal readonly BlobServiceClient _blobServiceClient = new(configuration["TPJ:Backup:Store:AzureBlobStorage:ConnectionString"]);

    internal BlobClient GenerateNewBlobClient(string containerName, CancellationToken cancellationToken)
    {
        var container = GetContainerClient(containerName);
        return GetBlobClient(container, $"{Guid.NewGuid()}-{DateTimeOffset.UtcNow.Ticks}");
    }

    internal static BlobClient GetBlobClient(BlobContainerClient container, string fileName)
    {
        var blobClient = container.GetBlobClient(fileName);
        return blobClient;
    }

    internal BlobContainerClient GetContainerClient(string containerName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        return containerClient;
    }

    internal async Task<string?> FindBlobByTagsAsync(BlobContainerClient container, BlobSearch search, CancellationToken cancellationToken) =>
        (await FindBlobsByTagsAsync(container, search, cancellationToken)).FirstOrDefault();

    internal static async Task<List<string>> FindBlobsByTagsAsync(BlobContainerClient container, BlobSearch search, CancellationToken cancellationToken)
    {
        var result = new List<string>();

        var queryString = search.GenerateQuery();

        if (queryString is null)
        {
            await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
                result.Add(blob.Name);
        }
        else
        {
            await foreach (var blob in container.FindBlobsByTagsAsync(queryString, cancellationToken))
                result.Add(blob.BlobName);
        }

        return result;
    }

    internal static bool IsCompressed(IDictionary<string, string> metadata)
    {
        metadata.TryGetValue("Compressed", out var sCompressed);
        return bool.TrueString.Equals(sCompressed, StringComparison.OrdinalIgnoreCase);
    }

    internal static string CleanTagValue(string value) =>
        value.Replace("'", string.Empty)
        .Replace("/", "-_-")
        .Replace("\\", "_-_");
}

internal class BlobSearch
{
    public string? FileExtension { get; set; }
    public string? RelativePath { get; set; }
    public string? FileName { get; set; }

    public string? GenerateQuery()
    {
        var queryItems = new List<string>();

        if (FileExtension != null)
            queryItems.Add($"FileExtension = '{AzureBlobStorageBase.CleanTagValue(FileExtension)}'");

        if (RelativePath != null)
            queryItems.Add($"RelativePath = '{AzureBlobStorageBase.CleanTagValue(RelativePath)}'");

        if (FileName != null)
            queryItems.Add($"FileName = '{AzureBlobStorageBase.CleanTagValue(FileName)}'");

        if (queryItems.Count == 0)
            return null;

        return string.Join(" && ", queryItems);
    }
}