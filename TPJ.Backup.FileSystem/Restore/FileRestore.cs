using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.FileSystem.Restore;

public class FileRestore(IConfiguration configuration,
    IFileRestoreSettings _settings,
    ILogger<FileRestore> _logger) : FileSystemBase(configuration), IFileRestoreManager
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

        var fileNames = GetAllFiles(settings.ContainerName, new FileSearch()
        {
            FileExtension = settings.FilterFileExtension,
            RelativePath = settings.FilterRelativePath,
            FileName = settings.FilterFileName
        });

        foreach (var fileName in fileNames)
        {
            var restorePath = GetRestorePath(settings.ContainerName, settings.RestoreToPath, fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(restorePath)!);

            if (IsCompressed(fileName))
            {
                _logger.LogInformation("Decompressing and restoring file: {FileName} to {RestorePath}", fileName, restorePath);
                restorePath = restorePath[..^3];
                var fileBytes = await File.ReadAllBytesAsync(fileName, cancellationToken);
                var decompressedData = await GZip.DecompressAsync(fileBytes, cancellationToken);
                await File.WriteAllBytesAsync(restorePath, decompressedData, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Copying file: {FileName} to {RestorePath}", fileName, restorePath);
                File.Copy(fileName, restorePath, true);
            }
        }

        _logger.LogInformation("Restore process completed for container: {ContainerName}", settings.ContainerName);
    }

    private string GetRestorePath(string containerName, string baseFolderPath, string filePath)
    {
        var relativePath = filePath.Substring($"{_backupDirectory}\\{containerName}".Length);
        return $"{baseFolderPath}{relativePath}";
    }

    private static bool IsCompressed(string fileName) => fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
}
