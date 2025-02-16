using Microsoft.Extensions.Configuration;

namespace TPJ.Backup.Store.AzureBlobStorage.Restore.Basic;

public interface IFileRestoreSettings
{
    IEnumerable<FolderRestoreSettings> Folders { get; }
}

public class FileRestoreSettings : IFileRestoreSettings
{
    public IEnumerable<FolderRestoreSettings> Folders { get; init; }

    public FileRestoreSettings(IConfiguration configuration)
    {
        var foldersSection = configuration.GetRequiredSection("TPJ:Backup:Configuration:AzureBlobStorage:Restore:Basic:Folders");

        Folders = foldersSection.GetChildren().Select(x => new FolderRestoreSettings
        {
            ContainerName = x["ContainerName"]!,
            RestoreToPath = x["RestoreToPath"]!,
            FilterRelativePath = x["Filter:RelativePath"],
            FilterFileExtension = x["Filter:FileExtension"],
            FilterFileName = x["Filter:FileName"],
        }).ToList();
    }
}

public record FolderRestoreSettings
{
    public required string ContainerName { get; set; }
    public required string RestoreToPath { get; set; }
    public string? FilterRelativePath { get; set; }
    public string? FilterFileExtension { get; set; }
    public string? FilterFileName { get; set; }
}
