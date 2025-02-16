using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.AzureBlobStorage.Retention;

public class FileRetention(IConfiguration configuration,
    IFileRetentionSettings _settings,
    ILogger<FileRetention> _logger) : AzureBlobStorageBase(configuration), IFileRetentionManager
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File retention process started.");

        foreach (var folderConfiguration in _settings.Folders)
        {
            _logger.LogInformation("Processing folder: {FolderName}", folderConfiguration.ContainerName);
            await DeleteFilesIfRetentionExpiredAsync(folderConfiguration, cancellationToken);
        }

        _logger.LogInformation("File retention process completed.");
    }

    private async Task DeleteFilesIfRetentionExpiredAsync(FolderRetentionSettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting deletion of files if retention expired for container: {ContainerName}", settings.ContainerName);

        var container = GetContainerClient(settings.ContainerName);
        var blobNames = await FindBlobsByTagsAsync(container, new()
        {
            FileExtension = settings.FileExtension
        }, cancellationToken);

        foreach (var blobName in blobNames)
        {
            var blobClient = GetBlobClient(container, blobName);

            if (await RetentionHasExpiredAsync(settings.RetentionDays, blobClient, cancellationToken))
            {
                _logger.LogInformation("Deleting blob: {BlobName}", blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            else
            {
                _logger.LogInformation("Retention not expired for blob: {BlobName}", blobName);
            }
        }

        _logger.LogInformation("Completed deletion of files if retention expired for container: {ContainerName}", settings.ContainerName);
    }

    private static async Task<bool> RetentionHasExpiredAsync(int retentionDays, BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        var blobProperties = await blobClient.GetPropertiesAsync(null, cancellationToken);
        var deleteAfterDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        blobProperties.Value.Metadata.TryGetValue("LastWriteTimeUtc", out var sLastWriteTimeUtc);

        // If there is a LastWriteTimeUtc tag, then both that and the CreatedOn date must be checked
        // if any of them are younger than the deleteAfterDate, then the file should not be deleted
        if (!string.IsNullOrWhiteSpace(sLastWriteTimeUtc)
            && DateTimeOffset.TryParse(sLastWriteTimeUtc, out var lastWriteTimeUtc))
        {
            if (lastWriteTimeUtc > deleteAfterDate)
                return false;
        }

        return blobProperties.Value.CreatedOn < deleteAfterDate;
    }
}
