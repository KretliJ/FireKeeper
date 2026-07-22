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

        public static GoogleDriveConfig GetGoogleDriveConfig()
        {
            // Try to load from user config first (AppData)
            if (File.Exists(UserConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(UserConfigPath);
                    var result = JsonConvert.DeserializeObject<JObject>(json);
                    return result["GoogleDrive"].ToObject<GoogleDriveConfig>();
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
                    return result["GoogleDrive"].ToObject<GoogleDriveConfig>();
                }
                catch { }
            }

            // If no config found, check environment variables
            string clientId = Environment.GetEnvironmentVariable("FIREKEEPER_GOOGLE_CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("FIREKEEPER_GOOGLE_CLIENT_SECRET");

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                return new GoogleDriveConfig
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };
            }

            throw new Exception(
                "Google Drive credentials not found!\n\n" +
                "Please create one of the following:\n" +
                "1. Copy appsettings.example.json to appsettings.json and add your credentials\n" +
                "2. Or set environment variables:\n" +
                "   - FIREKEEPER_GOOGLE_CLIENT_ID\n" +
                "   - FIREKEEPER_GOOGLE_CLIENT_SECRET"
            );
        }

        public static void SaveUserConfig(GoogleDriveConfig config)
        {
            string directory = Path.GetDirectoryName(UserConfigPath);
            Directory.CreateDirectory(directory);

            var json = new
            {
                GoogleDrive = new
                {
                    config.ClientId,
                    config.ClientSecret
                }
            };

            string jsonString = JsonConvert.SerializeObject(json, Formatting.Indented);
            File.WriteAllText(UserConfigPath, jsonString);
        }
    }

    public class GoogleDriveConfig
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public bool DebugEnabled { get; set; }

        public bool IsValid => 
            !string.IsNullOrEmpty(ClientId) && 
            !string.IsNullOrEmpty(ClientSecret);
    }
}