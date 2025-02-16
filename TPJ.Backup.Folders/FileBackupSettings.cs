using Microsoft.Extensions.Configuration;
using TPJ.Backup.Shared;

namespace TPJ.Backup;

public class FileBackupSettings : IFileBackupSettings
{
    public IEnumerable<IFolderBackupSettings> Folders { get; init; }

    public FileBackupSettings(IConfiguration configuration)
    {
        var backupSection = configuration.GetRequiredSection("TPJ:Backup:Configuration:Backup");
        var foldersSection = backupSection.GetRequiredSection("Folders");

        Folders = foldersSection.GetChildren().Select(x => new FolderBackupSettings
        {
            ContainerName = x["ContainerName"]!,
            FolderPath = x["FolderPath"]!,
            FileExtension = x["FileExtension"],
            RemoveOnDelete = bool.Parse(x["RemoveOnDelete"] ?? "false"),
            CompressFiles = bool.Parse(x["CompressFiles"] ?? "false")
        }).ToList();
    }
}

public class FolderBackupSettings() : IFolderBackupSettings
{
    public required string ContainerName { get; set; }
    public required string FolderPath { get; set; }
    public string? FileExtension { get; set; }
    public bool RemoveOnDelete { get; set; }
    public bool CompressFiles { get; set; }
}
