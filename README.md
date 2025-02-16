# TPJ.Backup
A simple backup of files onto another file system (be it a disk, or Azure ect), this is targeted at your unique scenarios for backing up (as if it isn’t unique you should be using a 'normal' paid for backup solution), this means it is expected you will take this code and change it to suit your needs, this project is meant to give you a good starting point leaving you wish coding your uniqueness and not the basic monitoring of files.

Within the configuration file (`appsettings.json`) in the example test console app you will find the configuration settings for File system to file system (disk to disk) and file system to Azure blob storage with one of just normal file system backing up and an example of a more unique setup for MSSQL server backup.

 `TPJ:Backup:Store` contains the settings for the location of the backed up files so for file system this is the backup file path and for Azure its the blob storage connection string.

 `TPJ:Backup:Configuration` contains the settings for backing up, retention, and restore. 
 `Backup` is the same no matter what type of store you are backing up to as this is the file path you are monitoring and what you are monitoring for, this will then fire an event to your back up class informing it that a file has been created / updated / deleted and you decide what to do with that information.
 `Retention` is used if you are not using `RemoveOnDelete` within the backup this will normal be used to set a number of days after which you want the file deleted from your backup store
 `Restore` contains the settings needed to restore your files from the backup store into the location you wish to restore the files

 For the example backup types in this project you should take a look at main logic classes (there arent many!), this will give you a clear overview of how to make you own versions for your own need or tweak it to make it work for you.

 1. `TPJ.Backup` -> `FileBackupManager.cs` this contains all the monitoring logic, you shouldnt need to change this no matter what you are doing for your backup

 2. `TPJ.Backup.Store.FileSystem.Backup` -> `FileBackup.cs` or `TPJ.Backup.Store.AzureBlobStorage.Backup` -> `FileBackup.cs` this contains the logic for backing up the file depending on the type of event that has happend on the file.
 
 3. `TPJ.Backup.Store.FileSystem.Retention` -> `FileRetention.cs` or `TPJ.Backup.Store.AzureBlobStorage.Retention` -> `FileRetention.cs` this contains the logic for deleting the files after X number of days.
 
 4. `TPJ.Backup.Store.FileSystem.Restore` -> `FileRestore.cs` or `TPJ.Backup.Store.AzureBlobStorage.Restore.Basic` -> `FileRestore.cs` or `TPJ.Backup.Store.AzureBlobStorage.Restore.MSSQL` -> `MSSQLFileRestore.cs` this contains the logic for restoring the files from the backup.