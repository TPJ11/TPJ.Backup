using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TPJ.Backup.Shared;

namespace TPJ.Backup;

public class FolderListener(IFolderBackupSettings configuration, 
    ILogger<FolderListener>? _logger = null)
{
    public IFolderBackupSettings Configuration => configuration;
    private FileSystemWatcher? _fileSystemWatcher;
    private ConcurrentQueue<FileChange>? _fileChangeQueue;

    public void StartWatching(ConcurrentQueue<FileChange> fileChangeQueue)
    {
        if (_fileSystemWatcher != null)
        {
            _logger?.LogWarning("FileSystemWatcher is already running.");
            return;
        }

        _fileChangeQueue = fileChangeQueue;

        if (string.IsNullOrWhiteSpace(Configuration.FileExtension))
        {
            _fileSystemWatcher = new FileSystemWatcher(Configuration.FolderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
        }
        else
        {
            _fileSystemWatcher = new FileSystemWatcher(Configuration.FolderPath, $"*.{Configuration.FileExtension}")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
        }

        _fileSystemWatcher.Changed += OnChanged;
        _fileSystemWatcher.Renamed += OnRename;
        _fileSystemWatcher.Deleted += OnDeleted;
        _fileSystemWatcher.EnableRaisingEvents = true;
        _fileSystemWatcher.IncludeSubdirectories = true;

        _logger?.LogInformation("Started watching folder: {FolderPath}", Configuration.FolderPath);
    }

    public void QueueAllExistingFiles(ConcurrentQueue<FileChange> fileChangeQueue)
    {
        var files = GetAllFiles();
        foreach (var filePath in files)
        {
            fileChangeQueue.Enqueue(new()
            {
                Settings = Configuration,
                FileInfo = new FileInfo(filePath),
                RelativePath = filePath.Replace(Configuration.FolderPath, string.Empty)
            });
        }
        _logger?.LogInformation("Queued all existing files in folder: {FolderPath}", Configuration.FolderPath);
    }

    public void StopWatching()
    {
        _fileSystemWatcher?.Dispose();
        _fileChangeQueue = null;
        _logger?.LogInformation("Stopped watching folder: {FolderPath}", Configuration.FolderPath);
    }

    private List<string> GetAllFiles()
    {
        var searchPattern = Configuration.FileExtension is not null
            ? $"*.{Configuration.FileExtension}"
            : "*.*";

        return Directory.GetFiles(Configuration.FolderPath,
               searchPattern,
               SearchOption.AllDirectories)
           .ToList();
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {        
        if (!Configuration.RemoveOnDelete || _fileChangeQueue is null || e.ChangeType == WatcherChangeTypes.Renamed)
            return;

        _fileChangeQueue.Enqueue(new()
        {
            Settings = Configuration,
            ChangeType = e.ChangeType,
            FileInfo = new FileInfo(e.FullPath),
            RelativePath = GetRelativePath(e.FullPath, Configuration.FolderPath)
        });

        _logger?.LogInformation("File Deleted: {FullPath}", e.FullPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (_fileChangeQueue is null || e.ChangeType == WatcherChangeTypes.Renamed)
            return;

        _fileChangeQueue.Enqueue(new()
        {
            Settings = Configuration,
            ChangeType = e.ChangeType,
            FileInfo = new FileInfo(e.FullPath),
            RelativePath = GetRelativePath(e.FullPath, Configuration.FolderPath)
        });

        _logger?.LogInformation("File changed: {FullPath}", e.FullPath);
    }

    private void OnRename(object sender, RenamedEventArgs e)
    {
        if (_fileChangeQueue is null)
            return;

        _fileChangeQueue.Enqueue(new()
        {
            Settings = Configuration,
            ChangeType = e.ChangeType,
            FileInfo = new FileInfo(e.FullPath),
            RelativePath = GetRelativePath(e.FullPath, Configuration.FolderPath),
            OldRelativePath = GetRelativePath(e.OldFullPath, Configuration.FolderPath),
            OldFileName = Path.GetFileName(e.OldFullPath)
        });

        _logger?.LogInformation("File renamed from {OldFullPath} to {FullPath}", e.OldFullPath, e.FullPath);
    }

    private static string GetRelativePath(string fullFilePath, string baseFolderPath) =>
        fullFilePath.Replace(baseFolderPath, string.Empty);
}
