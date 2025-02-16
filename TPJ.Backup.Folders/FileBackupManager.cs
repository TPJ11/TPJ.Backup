using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TPJ.Backup.Shared;

namespace TPJ.Backup;

public class FileBackupManager(
    IFileBackupStore _fileBackup,
    IFileBackupSettings _settings,
    ILogger<FileBackupManager>? _logger = null,
    ILogger<FolderListener>? loggerFolderListener = null) : IFileBackupManager, IDisposable
{
    private List<FolderListener> _folders = [];
    private readonly ConcurrentQueue<FileChange> _fileChangeQueue = new();

    public async Task MonitorAndBackupAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting MonitorAndBackupAsync");
        Initialize();
        await HandleFileChangesAsync(cancellationToken);
    }

    private void Initialize()
    {
        _logger?.LogInformation("Initializing folder listeners");
        _folders = _settings.Folders.Select(x => new FolderListener(x, loggerFolderListener)).ToList();

        if (_folders is null || _folders.Count == 0)
        {
            _logger?.LogWarning("No folders to monitor");
            return;
        }

        foreach (var folder in _folders)
        {
            _logger?.LogInformation($"Initializing folder {folder.Configuration.FolderPath}");
            folder.StartWatching(_fileChangeQueue);
            folder.QueueAllExistingFiles(_fileChangeQueue);
        }
    }

    private async Task HandleFileChangesAsync(CancellationToken cancellationToken)
    {
        while (true && !cancellationToken.IsCancellationRequested)
        {
            if (TryDequeueFileChange(out var fileChange))
            {
                _logger?.LogInformation("{ChangeType} file: {FileInfoFullName}",
                    fileChange.ChangeType.ToDescription(),
                    fileChange.FileInfo.FullName);
                await _fileBackup.ProcessChangeAsync(fileChange, cancellationToken);
            }
            else
            {
                _logger?.LogDebug("No file changes detected, waiting...");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private bool TryDequeueFileChange(out FileChange fileChange)
    {
        var success = _fileChangeQueue.TryDequeue(out fileChange!);

        if (!success)
        {
            _logger?.LogDebug("No file changes in queue");
            return false;
        }

        // If the file is locked, re-enqueue it at the back of the queue
        if (IsFileLocked(fileChange.FileInfo.FullName))
        {
            _logger?.LogWarning("File is locked, re-enqueuing: {FileName}", fileChange.FileInfo.FullName);
            _fileChangeQueue.Enqueue(fileChange);
            return false;
        }

        return true;
    }

    private static bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            stream.Close();
        }
        catch (IOException)
        {
            //the file is unavailable because it is:
            //still being written to
            //or being processed by another thread
            //or does not exist (has already been processed)
            return true;
        }

        //file is not locked
        return false;
    }

    public void Dispose()
    {
        if (_folders is not null)
        {
            foreach (var folder in _folders)
            {
                folder.StopWatching();
            }
        }
        _logger?.LogInformation("Disposed FileManagerBackup");
    }
}
