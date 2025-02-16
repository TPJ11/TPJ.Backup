namespace TPJ.Backup.Shared;

public interface IFileRetentionManager
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
