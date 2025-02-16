using Microsoft.Extensions.DependencyInjection;
using TPJ.Backup.Shared;

namespace TPJ.Backup;

public static class FileBackupServiceCollectionExtensions
{
    public static void AddTPJBackupManager<TFileBackupStore>(this IServiceCollection services)
        where TFileBackupStore : class, IFileBackupStore
    {
        services.AddSingleton<IFileBackupStore, TFileBackupStore>();

        services.AddSingleton<IFileBackupSettings, FileBackupSettings>();
        services.AddSingleton<IFileBackupManager, FileBackupManager>();
    }
}
