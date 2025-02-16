using TPJ.Backup.Shared;

namespace TPJ.Backup;

public class FileChange : IFileChange
{
    public required IFolderBackupSettings Settings { get; set; }
    public WatcherChangeTypes? ChangeType { get; set; }
    public required FileInfo FileInfo { get; set; }
    public required string RelativePath { get; set; }
    public string? OldRelativePath { get; set; }
    public string? OldFileName { get; set; }
}
