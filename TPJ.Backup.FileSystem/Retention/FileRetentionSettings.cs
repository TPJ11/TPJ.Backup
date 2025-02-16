using Microsoft.Extensions.Configuration;
using TPJ.Backup.Shared;

namespace TPJ.Backup.Store.FileSystem.Retention;

public interface IFileRetentionSettings
{
    IEnumerable<FolderRetentionSettings> Folders { get; }
}

public class FileRetentionSettings : IFileRetentionSettings
{
    public IEnumerable<FolderRetentionSettings> Folders { get; init; }

    public FileRetentionSettings(IConfiguration configuration)
    {
        var foldersSection = configuration.GetRequiredSection("TPJ:Backup:Configuration:FileSystem:Retention:Folders");

        Folders = foldersSection.GetChildren().Select(x => new FolderRetentionSettings
        {
            ContainerName = x["ContainerName"]!,
            FileExtension = x["FileExtension"],
            RetentionDays = int.Parse(x["RetentionDays"] ?? "30")
        }).ToList();
    }
}

public class FolderRetentionSettings
{
    public required string ContainerName { get; set; }
    public string? FileExtension { get; set; }
    public required int RetentionDays { get; set; }
}
