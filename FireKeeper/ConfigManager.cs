// ConfigManager.cs
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FireKeeper
{
    public static class ConfigManager
    {
        private static string ConfigPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "appsettings.json"
        );

        private static string UserConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FireKeeper",
            "appsettings.json"
        );

        public static AppSettings GetAppSettings()
        {
            // Try to load from user config first (AppData)
            if (File.Exists(UserConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(UserConfigPath);
                    var result = JsonConvert.DeserializeObject<JObject>(json);
                    return result.ToObject<AppSettings>() ?? new AppSettings();
                }
                catch { }
            }

            // Fallback to local config
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var result = JsonConvert.DeserializeObject<JObject>(json);
                    return result.ToObject<AppSettings>() ?? new AppSettings();
                }
                catch { }
            }

            return new AppSettings();
        }

        public static void SaveUserConfig(AppSettings settings)
        {
            string directory = Path.GetDirectoryName(UserConfigPath);
            Directory.CreateDirectory(directory);

            string jsonString = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(UserConfigPath, jsonString);
        }
    }

    public class AppSettings
    {
        public bool DebugEnabled { get; set; }
    }
}