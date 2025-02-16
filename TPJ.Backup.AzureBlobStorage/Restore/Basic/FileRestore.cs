using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.AzureBlobStorage.Restore.Basic;

public class FileRestore(IConfiguration configuration,
    IFileRestoreSettings _settings,
    ILogger<FileRestore> _logger) : AzureBlobStorageBase(configuration), IFileRestoreManager
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting file restore process.");
        foreach (var folderSettings in _settings.Folders)
        {
            _logger.LogInformation("Restoring folder: {Folder}", folderSettings.ContainerName);
            await RestoreAsync(folderSettings, cancellationToken);
            _logger.LogInformation("Restored folder: {Folder}", folderSettings.ContainerName);
        }
        _logger.LogInformation("File restore process completed.");
    }

    private async Task RestoreAsync(FolderRestoreSettings settings, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting restore process for container: {ContainerName}", settings.ContainerName);

        var container = GetContainerClient(settings.ContainerName);
        var blobNames = await FindBlobsByTagsAsync(container, new()
        {
            FileExtension = settings.FilterFileExtension,
            FileName = settings.FilterFileName,
            RelativePath = settings.FilterRelativePath,
        }, cancellationToken);

        _logger.LogInformation("Found {BlobCount} blobs to restore.", blobNames.Count);

        foreach (var blobName in blobNames)
        {
            _logger.LogInformation("Restoring blob: {BlobName}", blobName);

            var blobClient = GetBlobClient(container, blobName);
            var blobProperties = await blobClient.GetPropertiesAsync(null, cancellationToken);

            var restorePath = GetRestorePath(settings.RestoreToPath, blobProperties.Value.Metadata);

            Directory.CreateDirectory(Path.GetDirectoryName(restorePath)!);

            if (IsCompressed(blobProperties.Value.Metadata))
            {
                _logger.LogInformation("Blob {BlobName} is compressed. Decompressing...", blobName);

                using var stream = new MemoryStream();
                await blobClient.DownloadToAsync(stream, cancellationToken);

                var decompressedData = await GZip.DecompressAsync(stream, cancellationToken);
                await File.WriteAllBytesAsync(restorePath, decompressedData, cancellationToken);

                _logger.LogInformation("Blob {BlobName} decompressed and restored to {RestorePath}", blobName, restorePath);
            }
            else
            {
                await blobClient.DownloadToAsync(restorePath, cancellationToken);
                _logger.LogInformation("Blob {BlobName} restored to {RestorePath}", blobName, restorePath);
            }
        }

        _logger.LogInformation("Restore process completed for container: {ContainerName}", settings.ContainerName);
    }

    private static string GetRestorePath(string baseFolderPath, IDictionary<string, string> metadata)
    {
        metadata.TryGetValue("RelativePath", out var relativePath);
        return $"{baseFolderPath}{relativePath}";
    }
}
