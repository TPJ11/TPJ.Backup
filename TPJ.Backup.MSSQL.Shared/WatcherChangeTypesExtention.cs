namespace TPJ.Backup.Shared;

public static class WatcherChangeTypesExtention
{
    public static string? ToDescription(this WatcherChangeTypes? changeType) =>
      changeType switch
      {
          WatcherChangeTypes.Created => "Created",
          WatcherChangeTypes.Changed => "Changed",
          WatcherChangeTypes.Deleted => "Deleted",
          WatcherChangeTypes.Renamed => "Renamed",
          WatcherChangeTypes.All => "All",
          _ => "Checking",
      };
}
