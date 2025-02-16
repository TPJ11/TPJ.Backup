namespace TPJ.Backup;
public interface IFileBackupManager
{
    Task MonitorAndBackupAsync(CancellationToken cancellationToken = default);

    void Dispose();
}