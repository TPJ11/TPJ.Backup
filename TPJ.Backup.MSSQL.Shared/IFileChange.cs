namespace TPJ.Backup.Shared;

public interface IFileChange
{
    IFolderBackupSettings Settings { get; }
    WatcherChangeTypes? ChangeType { get; }
    FileInfo FileInfo { get; }
    string RelativePath { get; }
    string? OldRelativePath { get; }
    string? OldFileName { get; }
}