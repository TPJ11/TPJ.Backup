using Microsoft.Extensions.Configuration;

namespace TPJ.Backup.Store.FileSystem;

public abstract class FileSystemBase(IConfiguration configuration)
{
    internal readonly string _backupDirectory = configuration["TPJ:Backup:Store:FileSystem:BackupDirectory"]!;

    internal List<string> GetAllFiles(string containerName, FileSearch filters)
    {
        var directory = _backupDirectory + "\\" + containerName;
        if (!string.IsNullOrEmpty(filters.RelativePath))
            directory += filters.RelativePath;

        if (filters.FileName is not null)
        {
            var result = new List<string>();
            result.AddRange(Directory.GetFiles(directory, filters.FileName, SearchOption.AllDirectories).ToList());
            result.AddRange(Directory.GetFiles(directory, $"{filters.FileName}.gz", SearchOption.AllDirectories).ToList());
            return result.Distinct().ToList();
        }
        
        if (filters.FileExtension is not null)
        {
            var result = new List<string>();
            result.AddRange(Directory.GetFiles(directory, $"*.{filters.FileExtension}", SearchOption.AllDirectories).ToList());
            result.AddRange(Directory.GetFiles(directory, $"*.{filters.FileExtension}.gz", SearchOption.AllDirectories).ToList());
            return result.Distinct().ToList();
        }

        return Directory.GetFiles(directory, $"*.*", SearchOption.AllDirectories).ToList();
    }

    internal string GenerateBackupFilePath(string containerName, string relativePath) =>
        _backupDirectory + "\\" + containerName + relativePath;
}

internal class FileSearch
{
    public string? FileExtension { get; set; }
    public string? RelativePath { get; set; }
    public string? FileName { get; set; }
}
