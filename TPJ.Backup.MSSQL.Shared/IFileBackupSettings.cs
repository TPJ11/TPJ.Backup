namespace TPJ.Backup.Shared;

public interface IFileBackupSettings
{
    IEnumerable<IFolderBackupSettings> Folders { get; }
}

public interface IFolderBackupSettings
{
    string? FileExtension { get; }
    string FolderPath { get; }
    string ContainerName { get; }
    bool RemoveOnDelete { get; }
    bool CompressFiles { get; }
}