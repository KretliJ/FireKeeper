// Config.cs - Persisted application settings
using System.Collections.Generic;

namespace FireKeeper
{
    public enum ThemeMode
    {
        Dark,
        Light
    }

    public class Config
    {
        public int BackupIntervalHours { get; set; }
        public int MaxBackups { get; set; }
        public string FirefoxProfilePath { get; set; }
        public string LastBackup { get; set; }
        public string SyncFolderPath { get; set; }
        public List<string> IncludeFolders { get; set; }
        public List<string> ExcludeFolders { get; set; }
        public List<string> ExcludeExtensions { get; set; }
    }
}
