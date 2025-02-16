using Microsoft.Extensions.Configuration;

namespace TPJ.Backup.Store.AzureBlobStorage.Restore.MSSQL;

public interface IMSSQLFileRestoreSettings
{
    List<MSSQLFolderRestoreSettings> Folders { get; }
}

public class MSSQLFileRestoreSettings : IMSSQLFileRestoreSettings
{
    public List<MSSQLFolderRestoreSettings> Folders { get; init; }

    public MSSQLFileRestoreSettings(IConfiguration configuration)
    {
        var foldersSection = configuration.GetRequiredSection("TPJ:Backup:Configuration:AzureBlobStorage:Restore:MSSQL:Folders");

        Folders = [];
        foreach (var x in foldersSection.GetChildren())
        {
            var folder = new MSSQLFolderRestoreSettings
            {
                BackupContainerName = x["Backup:ContainerName"]!,
                BackupRestorePath = x["Backup:RestorePath"]!,
                TransactionLogContainerName = x["TransactionLog:ContainerName"],
                TransactionLogRestorePath = x["TransactionLog:RestorePath"],
                RestoreType = x["RestoreType"]!,
                IncludeDatabases = [],
                ExcludeDatabases = []
            };

            var includeDatabases = x.GetSection("IncludeDatabases").GetChildren();
            foreach (var y in includeDatabases)            
                folder.IncludeDatabases.Add(y.Value!);

            var excludeDatabases = x.GetSection("ExcludeDatabases").GetChildren();
            foreach (var y in excludeDatabases)
                folder.ExcludeDatabases.Add(y.Value!);

            Folders.Add(folder);
        }
    }
}

public record MSSQLFolderRestoreSettings
{
    public required string BackupContainerName { get; set; }
    public required string BackupRestorePath { get; set; }

    public string? TransactionLogContainerName { get; set; }
    public string? TransactionLogRestorePath { get; set; }

    /// <summary>
    /// Latest = Latest backup and log files since this backup
    /// All = All backup and log files downloaded
    /// </summary>
    public required string RestoreType { get; set; }

    /// <summary>
    /// List of databases to download, if not set all 
    /// databases that are not within the <see cref="ExcludeDatabases"/> will be downloaded
    /// </summary>
    public List<string> IncludeDatabases { get; set; } = [];

    /// <summary>
    /// List of databases to ignore
    /// </summary>
    public List<string> ExcludeDatabases { get; set; } = [];
}
