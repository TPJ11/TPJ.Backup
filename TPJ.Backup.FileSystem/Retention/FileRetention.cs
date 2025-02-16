using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.FileSystem.Retention;

public class FileRetention(IConfiguration configuration,
    IFileRetentionSettings _settings,
    ILogger<FileRetention> _logger) : FileSystemBase(configuration), IFileRetentionManager
{
#pragma warning disable CS1998
    public async Task RunAsync(CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        _logger.LogInformation("File retention process started.");

        foreach (var folderConfiguration in _settings.Folders)
        {
            _logger.LogInformation("Processing folder: {FolderName}", folderConfiguration.ContainerName);
            DeleteFilesIfRetentionExpiredAsync(folderConfiguration);
        }

        _logger.LogInformation("File retention process completed.");
    }

    private void DeleteFilesIfRetentionExpiredAsync(FolderRetentionSettings settings)
    {
        var fileNames = GetAllFiles(settings.ContainerName, new FileSearch()
        {
            FileExtension = settings.FileExtension
        });

        foreach (var fileName in fileNames)
        {
            var fileInfo = new FileInfo(fileName);
            if (RetentionHasExpired(settings.RetentionDays, fileInfo))
            {
                _logger.LogInformation("Deleting file: {fileName}", fileName);
                File.Delete(fileName);
            }
            else
                _logger.LogInformation("File {fileName} has not expired", fileName);
        }
    }

    private static bool RetentionHasExpired(int retentionDays, FileInfo fileInfo)
    {
        var deleteAfterDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        return fileInfo.LastWriteTimeUtc <= deleteAfterDate && fileInfo.CreationTimeUtc < deleteAfterDate;
    }
}
