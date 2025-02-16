using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.FileSystem.Backup;

public class FileBackup(IConfiguration configuration,
    ILogger<FileBackup> _logger) : FileSystemBase(configuration), IFileBackupStore
{
    public async Task ProcessChangeAsync(IFileChange fileChange, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing change: {ChangeType} for file: {FilePath}", fileChange.ChangeType.ToDescription(), fileChange.RelativePath);

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

            Delete(fileChange);
            return;
        }

        await UpdateAsync(fileChange, cancellationToken);
    }

    private void Delete(IFileChange fileChange)
    {
        var backupFilePath = GenerateBackupFilePath(fileChange.Settings.ContainerName, fileChange.RelativePath);

        if (File.Exists(backupFilePath))
        {
            _logger.LogInformation("Deleting backup file: {FilePath}", backupFilePath);
            File.Delete(backupFilePath);
        }

        var gzBackupFilePath = $"{backupFilePath}.gz";
        if (File.Exists(gzBackupFilePath))
        {
            _logger.LogInformation("Deleting compressed backup file: {FilePath}", gzBackupFilePath);
            File.Delete(gzBackupFilePath);
        }
    }

    private async Task UpdateAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        var backupFilePath = GenerateBackupFilePath(fileChange.Settings.ContainerName, fileChange.RelativePath);

        if (File.Exists(backupFilePath))
        {
            if (fileChange.ChangeType is null)
                return;

            _logger.LogInformation("Updating backup file: {FilePath}", backupFilePath);
            File.Delete(backupFilePath);
        }

        await CreateAsync(fileChange, cancellationToken);
    }

    private async Task RenameAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        var oldbackupFilePath = GenerateBackupFilePath(fileChange.Settings.ContainerName, fileChange.OldRelativePath!);
        var backupFilePath = GenerateBackupFilePath(fileChange.Settings.ContainerName, fileChange.RelativePath);

        if (!File.Exists(backupFilePath))
        {
            await CreateAsync(fileChange, cancellationToken);
            return;
        }

        _logger.LogInformation("Renaming backup file from: {OldFilePath} to: {NewFilePath}", oldbackupFilePath, backupFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
        File.Move(oldbackupFilePath, backupFilePath);
    }

    private async Task CreateAsync(IFileChange fileChange, CancellationToken cancellationToken)
    {
        var backupFilePath = GenerateBackupFilePath(fileChange.Settings.ContainerName, fileChange.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);

        if (fileChange.Settings.CompressFiles)
        {
            _logger.LogInformation("Creating compressed backup file: {FilePath}", backupFilePath);
            var compressedData = await GZip.CompressAsync(fileChange.FileInfo.FullName, cancellationToken);
            await File.WriteAllBytesAsync($"{backupFilePath}.gz", compressedData, cancellationToken);
            return;
        }

        _logger.LogInformation("Creating backup file: {FilePath}", backupFilePath);
        File.Copy(fileChange.FileInfo.FullName, backupFilePath);
    }
}
