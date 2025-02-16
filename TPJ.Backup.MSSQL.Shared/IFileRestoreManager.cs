namespace TPJ.Backup.Shared;

public interface IFileRestoreManager
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
