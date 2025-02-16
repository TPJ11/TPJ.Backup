namespace TPJ.Backup.Shared;

public interface IFileBackupStore
{
    Task ProcessChangeAsync(IFileChange fileChange, CancellationToken cancellationToken = default);
}
