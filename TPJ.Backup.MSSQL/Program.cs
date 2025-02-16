using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TPJ.Backup;
using TPJ.Backup.Shared;

namespace TPJ.BackupTest;

internal class Program
{
    private static IFileBackupManager _fileManagerBackup = default!;    
    private static IFileRetentionManager _fileManagerRetention = default!;    
    private static IFileRestoreManager _fileManagerRestore = default!;

    static async Task Main(string[] args)
    {
        Console.Title = "TPJ Backup Example";

        SetUp(args[0]);
        var mode = args[1].ToString();

        if (mode.Equals("backup", StringComparison.OrdinalIgnoreCase))
            await _fileManagerBackup.MonitorAndBackupAsync();

        if (mode.Equals("retention", StringComparison.OrdinalIgnoreCase))
            await _fileManagerRetention.RunAsync();

        if (mode.Equals("restore", StringComparison.OrdinalIgnoreCase))
            await _fileManagerRestore.RunAsync();
    }

    private static void SetUp(string type)
    {
        var services = new ServiceCollection();

        var builder = new ConfigurationBuilder().AddEnvironmentVariables();

        var configuration = builder.Build();

        builder = new ConfigurationBuilder()
              .SetBasePath(Environment.CurrentDirectory)
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddJsonFile($"appsettings.{configuration["ASPNETCORE_ENVIRONMENT"]}.json", optional: true)
              .AddEnvironmentVariables();

        configuration = builder.Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(configure =>
        {
            configure.AddConsole();  // Log to console
            configure.AddConfiguration(configuration.GetSection("Logging"));
        });

        if (type.StartsWith("Azure"))
        {
            services.AddTPJBackupManager<Backup.Store.AzureBlobStorage.Backup.FileBackup>();

            services.AddSingleton<Backup.Store.AzureBlobStorage.Retention.IFileRetentionSettings, Backup.Store.AzureBlobStorage.Retention.FileRetentionSettings>();
            services.AddSingleton<IFileRetentionManager, Backup.Store.AzureBlobStorage.Retention.FileRetention>();

            if (type.EndsWith("MSSQL"))
            {
                services.AddSingleton<Backup.Store.AzureBlobStorage.Restore.MSSQL.IMSSQLFileRestoreSettings, Backup.Store.AzureBlobStorage.Restore.MSSQL.MSSQLFileRestoreSettings>();
                services.AddSingleton<IFileRestoreManager, Backup.Store.AzureBlobStorage.Restore.MSSQL.MSSQLFileRestore>();
            }
            else if (type.EndsWith("Basic"))
            {
                services.AddSingleton<Backup.Store.AzureBlobStorage.Restore.Basic.IFileRestoreSettings, Backup.Store.AzureBlobStorage.Restore.Basic.FileRestoreSettings>();
                services.AddSingleton<IFileRestoreManager, Backup.Store.AzureBlobStorage.Restore.Basic.FileRestore>();
            }
        }
        else if (type.Equals("FileSystem"))
        {
            services.AddTPJBackupManager<Backup.Store.FileSystem.Backup.FileBackup>();

            services.AddSingleton<Backup.Store.FileSystem.Retention.IFileRetentionSettings, Backup.Store.FileSystem.Retention.FileRetentionSettings>();
            services.AddSingleton<IFileRetentionManager, Backup.Store.FileSystem.Retention.FileRetention>();

            services.AddSingleton<Backup.Store.FileSystem.Restore.IFileRestoreSettings, Backup.Store.FileSystem.Restore.FileRestoreSettings>();
            services.AddSingleton<IFileRestoreManager, Backup.Store.FileSystem.Restore.FileRestore>();
        }

        var serviceProvider = services.BuildServiceProvider();

        _fileManagerBackup = serviceProvider.GetRequiredService<IFileBackupManager>();
        _fileManagerRetention = serviceProvider.GetRequiredService<IFileRetentionManager>();
        _fileManagerRestore = serviceProvider.GetRequiredService<IFileRestoreManager>();
    }
}
