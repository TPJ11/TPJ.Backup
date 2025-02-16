using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.AzureBlobStorage.Restore.MSSQL;

public class MSSQLFileRestore(IConfiguration configuration,
    IMSSQLFileRestoreSettings _settings,
    ILogger<MSSQLFileRestore> _logger) : AzureBlobStorageBase(configuration), IFileRestoreManager
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting file restore process.");

        foreach (var folderSettings in _settings.Folders)
            await RestoreAsync(folderSettings, cancellationToken);

        _logger.LogInformation("File restore process completed.");
    }

    private async Task RestoreAsync(MSSQLFolderRestoreSettings settings, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting restore process for container: {BackupContainerName} and {TransactionLogContainerName}", settings.BackupContainerName, settings.TransactionLogContainerName);

        var databases = await GetDatabaseBlobsAsync(settings, cancellationToken);

        Directory.CreateDirectory(settings.BackupRestorePath);

        foreach (var database in databases)
        {
            foreach (var backup in database.Backups)
            {
                var restrorePath = Path.Combine(settings.BackupRestorePath, backup.FileName);
                _logger.LogInformation("Downloading backup file {FileName} to {RestorePath}", backup.FileName, restrorePath);
                await backup.Client.DownloadToAsync(restrorePath, cancellationToken: cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(settings.TransactionLogRestorePath))
            {
                foreach (var transactionLog in database.TransactionLogs)
                {
                    var restrorePath = Path.Combine(settings.TransactionLogRestorePath, transactionLog.DatabaseName);
                    Directory.CreateDirectory(restrorePath);
                    restrorePath = Path.Combine(restrorePath, transactionLog.FileName);
                    _logger.LogInformation("Downloading transaction log file {FileName} to {RestorePath}", transactionLog.FileName, restrorePath);
                    await transactionLog.Client.DownloadToAsync(restrorePath, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private async Task<List<BlobRestoreGroup>> GetDatabaseBlobsAsync(MSSQLFolderRestoreSettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching database blobs for container: {BackupContainerName}", settings.BackupContainerName);
        var backupContainer = GetContainerClient(settings.BackupContainerName);
        var backupBlobs = await GetBlobsAsync(settings, backupContainer, cancellationToken);

        var transactionLogBlobs = new List<BlobRestore>();

        if (!string.IsNullOrWhiteSpace(settings.TransactionLogContainerName)
            && !string.IsNullOrWhiteSpace(settings.TransactionLogRestorePath))
        {
            _logger.LogInformation("Fetching transaction log blobs for container: {TransactionLogContainerName}", settings.TransactionLogContainerName);
            var transactionLogContainer = GetContainerClient(settings.TransactionLogContainerName);
            transactionLogBlobs = await GetBlobsAsync(settings, transactionLogContainer, cancellationToken);
        }

        var blobGroups = new List<BlobRestoreGroup>();
        foreach (var backups in backupBlobs.GroupBy(x => x.DatabaseName))
        {
            var group = new BlobRestoreGroup
            {
                DatabaseName = backups.Key,
                Backups = [.. backups],
                TransactionLogs = transactionLogBlobs.Where(x => x.DatabaseName == backups.Key).ToList()
            };

            if (settings.RestoreType.Equals("Latest", StringComparison.OrdinalIgnoreCase))
            {
                var latestBackup = group.Backups.OrderByDescending(x => x.BackupDateTime).First();
                group.Backups = [latestBackup];
                group.TransactionLogs = group.TransactionLogs
                    .Where(x => x.BackupDateTime >= latestBackup.BackupDateTime)
                    .ToList();
            }

            blobGroups.Add(group);
        }

        return blobGroups;
    }

    private async Task<List<BlobRestore>> GetBlobsAsync(MSSQLFolderRestoreSettings settings,
        BlobContainerClient container, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching blobs from container: {ContainerName}", container.Name);
        var blobNames = await FindBlobsByTagsAsync(container, new(), cancellationToken);

        var blobClients = new List<BlobRestore>();
        foreach (var blobName in blobNames)
        {
            var client = GetBlobClient(container, blobName);

            var clientProperties = await client.GetPropertiesAsync(cancellationToken: cancellationToken);

            if (clientProperties.Value.Metadata.TryGetValue("Name", out var databaseName)
                && clientProperties.Value.Metadata.TryGetValue("FileName", out var fileName)
                && clientProperties.Value.Metadata.TryGetValue("BackupDateTime", out var sBackupDateTime)
                && DateTime.TryParse(sBackupDateTime, out var backupDateTime))
            {
                if (settings.ExcludeDatabases.Count != 0
                    && blobNames.Any(x => settings.ExcludeDatabases.Contains(databaseName)))
                    continue;

                if (settings.IncludeDatabases.Count != 0
                   && blobNames.Any(x => !settings.IncludeDatabases.Contains(databaseName)))
                    continue;

                blobClients.Add(new()
                {
                    Client = client,
                    FileName = fileName,
                    DatabaseName = databaseName,
                    BackupDateTime = backupDateTime
                });
            }
        }

        return blobClients;
    }
}

class BlobRestore
{
    public required BlobClient Client { get; set; }
    public required string DatabaseName { get; set; }
    public required string FileName { get; set; }
    public required DateTime BackupDateTime { get; set; }
}

class BlobRestoreGroup
{
    public required string DatabaseName { get; set; }
    public List<BlobRestore> Backups { get; set; } = [];
    public List<BlobRestore> TransactionLogs { get; set; } = [];
}
