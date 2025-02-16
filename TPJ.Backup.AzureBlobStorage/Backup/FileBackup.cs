using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.AzureBlobStorage.Backup;

public class FileBackup(IConfiguration configuration,
    ILogger<FileBackup> _logger) : AzureBlobStorageBase(configuration), IFileBackupStore
{
    public async Task ProcessChangeAsync(IFileChange fileChange, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing change: {ChangeType} for file: {FileInfoName}",
            fileChange.ChangeType.ToDescription(),
            fileChange.FileInfo.Name);

        if (fileChange.ChangeType == WatcherChangeTypes.Created)
        {
            await CreateAsync(fileChange, cancellationToken);
            return;
        }

        if (fileChange.ChangeType == WatcherChangeTypes.Renamed)
        {
            await RenameAsync(fileChange, cancellationToken);
            return;
        }

        if (fileChange.ChangeType == WatcherChangeTypes.Deleted)
        {
            if (!fileChange.Settings.RemoveOnDelete)
                return;

            await DeleteAsync(fileChange, cancellationToken);
            return;
        }

        await UpdateAsync(fileChange, cancellationToken);
    }

    private async Task DeleteAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting file: {FileName}", fileChange.FileInfo.Name);

        var blobName = await FindBlobByTagsAsync(
                        GetContainerClient(fileChange.Settings.ContainerName),
                        new BlobSearch
                        {
                            RelativePath = fileChange.RelativePath,
                            FileName = fileChange.FileInfo.Name
                        },
                        cancellationToken);

        if (blobName is null)
        {
            _logger.LogWarning("Blob not found for file: {FileName}", fileChange.FileInfo.Name);
            return;
        }

        var container = GetContainerClient(fileChange.Settings.ContainerName);
        var blobClient = GetBlobClient(container, blobName);

        await blobClient.DeleteAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted blob: {BlobName}", blobName);
    }

    private async Task UpdateAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating file: {FileName}", fileChange.FileInfo.Name);

        var blobName = await FindBlobByTagsAsync(
                        GetContainerClient(fileChange.Settings.ContainerName),
                        new BlobSearch
                        {
                            RelativePath = fileChange.RelativePath,
                            FileName = fileChange.FileInfo.Name
                        },
                        cancellationToken);

        if (blobName is null)
        {
            _logger.LogWarning("Blob not found for file: {FileName}, creating new blob", fileChange.FileInfo.Name);
            await CreateAsync(fileChange, cancellationToken);
            return;
        }

        if (fileChange.ChangeType is null)
            return;

        var container = GetContainerClient(fileChange.Settings.ContainerName);
        var blobClient = GetBlobClient(container, blobName);
        await UploadFileAsync(fileChange, blobClient, cancellationToken);
        _logger.LogInformation("Updated blob: {BlobName}", blobName);
    }

    private async Task RenameAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Renaming file: {OldFileName} to {FileName}", fileChange.OldFileName, fileChange.FileInfo.Name);

        var blobName = await FindBlobByTagsAsync(
                        GetContainerClient(fileChange.Settings.ContainerName),
                        new BlobSearch
                        {
                            RelativePath = fileChange.OldRelativePath,
                            FileName = fileChange.OldFileName
                        },
                        cancellationToken);

        if (blobName is null)
        {
            _logger.LogWarning("Blob not found for file: {OldFileName}, creating new blob", fileChange.OldFileName);
            await CreateAsync(fileChange, cancellationToken);
            return;
        }

        var container = GetContainerClient(fileChange.Settings.ContainerName);
        var blobClient = GetBlobClient(container, blobName);

        var blobProperties = await blobClient.GetPropertiesAsync(null, cancellationToken);

        var metaData = GenerateMetadata(fileChange);
        // Don't overwrite the compressed metadata as we arent updating the data of the file  
        blobProperties.Value.Metadata.TryGetValue("Compressed", out var compressed);
        metaData["Compressed"] = compressed ?? "false";

        await blobClient.SetMetadataAsync(metaData, cancellationToken: cancellationToken);
        await blobClient.SetTagsAsync(GenerateTags(fileChange), cancellationToken: cancellationToken);
        _logger.LogInformation("Renamed blob: {BlobName}", blobName);
    }

    private async Task CreateAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new blob for file: {FileName}", fileChange.FileInfo.Name);

        var blobClient = GenerateNewBlobClient(fileChange.Settings.ContainerName, cancellationToken);
        await UploadFileAsync(fileChange, blobClient, cancellationToken);
        _logger.LogInformation("Created new blob for file: {FileName}", fileChange.FileInfo.Name);
    }

    private static async Task UploadFileAsync(IFileChange fileChange, BlobClient blobClient, CancellationToken cancellationToken)
    {
        if (fileChange.Settings.CompressFiles)
        {
            var compressedData = await GZip.CompressAsync(fileChange.FileInfo.FullName, cancellationToken);

            using var stream = new MemoryStream(compressedData);
            await blobClient.UploadAsync(
                content: stream,
                options: new Azure.Storage.Blobs.Models.BlobUploadOptions()
                {
                    AccessTier = Azure.Storage.Blobs.Models.AccessTier.Cold,
                    Metadata = GenerateMetadata(fileChange),
                    Tags = GenerateTags(fileChange),
                },
                cancellationToken: cancellationToken);
        }
        else
        {
            await blobClient.UploadAsync(
                path: fileChange.FileInfo.FullName,
                options: new Azure.Storage.Blobs.Models.BlobUploadOptions()
                {
                    AccessTier = Azure.Storage.Blobs.Models.AccessTier.Cold,
                    Metadata = GenerateMetadata(fileChange),
                    Tags = GenerateTags(fileChange),
                },
                cancellationToken: cancellationToken);
        }
    }

    private static Dictionary<string, string> GenerateTags(IFileChange fileChange)
    {
        return new Dictionary<string, string>
        {
            { "Prefix", CleanTagValue(fileChange.Settings.ContainerName) },
            { "FileExtension", CleanTagValue(fileChange.FileInfo.Extension) },
            { "RelativePath", CleanTagValue(fileChange.RelativePath) },
            { "FileName", CleanTagValue(fileChange.FileInfo.Name) },

            { "Name", CleanTagValue(GetDatabaseNameFromFileName(fileChange.FileInfo.Name)) }
        };
    }

    private static Dictionary<string, string> GenerateMetadata(IFileChange fileChange)
    {
        return new Dictionary<string, string>
        {
            { "Compressed", fileChange.Settings.CompressFiles.ToString() },
            { "RelativePath", fileChange.RelativePath },
            { "FileExtension", fileChange.FileInfo.Extension },
            { "FileName", fileChange.FileInfo.Name },
            { "LastWriteTimeUtc", fileChange.FileInfo.LastWriteTimeUtc.ToString("dd/MM/yyyy HH:mm:ss") },

            { "Name", GetDatabaseNameFromFileName(fileChange.FileInfo.Name) },
            { "BackupDateTime", GetBackupDateTime(fileChange.FileInfo.Name).ToString("dd/MM/yyyy HH:mm:ss") },
        };
    }

    private static string GetDatabaseNameFromFileName(string fileName)
    {
        var databaseName = fileName.Contains("_backup_") ? fileName.Split("_backup_")[0] : fileName;
        return databaseName;
    }

    public static DateTime GetBackupDateTime(string fileName)
    {
        // Define the pattern to match the datetime part in the file name
        var pattern = @"(\d{4}_\d{2}_\d{2}_\d{6})";

        // Find the match using regex
        var match = Regex.Match(fileName, pattern);
        if (match.Success)
        {
            string dateTimeString = match.Value;
            // Parse the datetime string to a DateTime object
            return DateTime.ParseExact(dateTimeString, "yyyy_MM_dd_HHmmss", CultureInfo.InvariantCulture);
        }

        throw new ArgumentException("Invalid file name format.");
    }
}
