// BackupTrayContext.Core.cs - Tray icon lifecycle, configuration loading, startup registration
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace FireKeeper
{
    public partial class BackupTrayContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer backupTimer;
        private Config config;
        private string configPath;
        private bool isBackupRunning = false;
        private SettingsWindowHost settingsHost;
        private const string APP_NAME = "FireKeeper";
        public event Action<int, string> ProgressUpdate;

        private void ReportProgress(int percent, string status)
        {
            ProgressUpdate?.Invoke(percent, status);
        }

        public bool ShouldShowNotifications()
        {
            return settingsHost == null || !settingsHost.IsOpen;
        }

        private void CheckPendingBackupOnStartup()
        {
            if (string.IsNullOrEmpty(config.LastBackup)) return;
            
            DateTime lastTime = DateTime.ParseExact(config.LastBackup, "yyyyMMdd_HHmmss", null);
            DateTime nextScheduled = lastTime.AddHours(config.BackupIntervalHours);
            
            if (DateTime.Now >= nextScheduled)
            {
                DebugConsole.Log("Pending backup detected. Running now...");
                trayIcon.ShowBalloonTip(3000, APP_NAME, 
                    "⏰ Pending backup detected. Running now...", 
                    ToolTipIcon.Info);
                _ = Task.Run(() => PerformBackup());
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null)
                    {
                        DebugConsole.Log("Failed to open Registry key for startup.");
                        return;
                    }

                    if (enable)
                    {
                        key.SetValue("FireKeeper", Application.ExecutablePath);
                        DebugConsole.Log($"Added FireKeeper to startup: {Application.ExecutablePath}");
                    }
                    else
                    {
                        if (key.GetValue("FireKeeper") != null)
                        {
                            key.DeleteValue("FireKeeper", false);
                            DebugConsole.Log("Removed FireKeeper from startup.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Error setting startup: {ex.Message}");
            }
        }
        // Backup selection rules - files and folders to include
        private static readonly HashSet<string> IncludeFolders = new HashSet<string>
        {
            "bookmarkbackups",
            "browser-extension-data",
            "browser",
            "chrome",
            "datareporting",
            "extensions",
            "feeds",
            "gmp",
            "gmp-gmpopenh264",
            "gmp-widevinecdm",
            "healthreport",
            "minidumps",
            "pending-pings",
            "safebrowsing",
            "security_state",
            "sessionstore-backups",
            "signedInUser",
            "storage",
            "thumbnails",
            "xulstore",
            "containers.json",
            "cookies.sqlite",
            "cookies.sqlite-wal",
            "cookies.sqlite-shm",
            "favicons.sqlite",
            "favicons.sqlite-wal",
            "favicons.sqlite-shm",
            "formhistory.sqlite",
            "formhistory.sqlite-wal",
            "formhistory.sqlite-shm",
            "handlers.json",
            "key3.db",
            "key4.db",
            "logins.json",
            "permissions.sqlite",
            "permissions.sqlite-wal",
            "permissions.sqlite-shm",
            "places.sqlite",
            "places.sqlite-wal",
            "places.sqlite-shm",
            "prefs.js",
            "protections.sqlite",
            "protections.sqlite-wal",
            "protections.sqlite-shm",
            "search.json.mozlz4",
            "sessionstore.jsonlz4",
            "storage-sync-v2.sqlite",
            "storage-sync-v2.sqlite-wal",
            "storage-sync-v2.sqlite-shm",
            "xulstore.json"
        };

        private static readonly HashSet<string> ExcludeFolders = new HashSet<string>
        {
            "cache2",
            "cache",
            "OfflineCache",
            "weave",
            "storage\\default\\https+++",
            "storage\\temporary",
            "thumbnails\\failures",
            "thumbnails\\cache",
            "datareporting\\archived",
            "datareporting\\pending",
            "safebrowsing\\cache",
            "security_state\\cert-revocations"
        };

        private static readonly HashSet<string> ExcludeExtensions = new HashSet<string>
        {
            ".lock",
            ".tmp",
            ".temp",
            ".log",
            ".cache",
            ".wal",
            ".shm",
            ".bak",
            ".old",
            ".corrupt"
        };

        public BackupTrayContext()
        {
            // Load app config
            configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                APP_NAME, "config.json");
            LoadConfig();

            // Setup tray
            trayIcon = new NotifyIcon();
            trayIcon.Icon = GenerateIcon();
            trayIcon.Text = APP_NAME + " - Firefox Backup";
            trayIcon.Visible = true;

            trayIcon.BalloonTipClicked += (s, e) => OpenManager(s, e);

            // Build menu
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open Manager", null, OpenManager);
            menu.Items.Add("Backup Now", null, ManualBackup);
            menu.Items.Add("-");
            menu.Items.Add("Toggle Debug Console", null, ToggleDebugConsole);
            menu.Items.Add("Exit", null, Exit);
            trayIcon.ContextMenuStrip = menu;

            trayIcon.DoubleClick += (s, e) => OpenManager(s, e);

            // Start backup scheduler
            StartScheduler();

            // ✅ Check for pending backup on startup
            CheckPendingBackupOnStartup();

            // Show startup notification after a short delay
            ShowStartupNotification();
        }

        
        public void SetTestPaths(string profilePath, string backupPath)
        {
            if (config == null)
            {
                config = new Config();
            }
            config.FirefoxProfilePath = profilePath;
            config.SyncFolderPath = backupPath;
            config.MaxBackups = 10;
        }

        public BackupTrayContext(bool forTesting)
        {
            config = new Config
            {
                BackupIntervalHours = 24,
                MaxBackups = 10,
                FirefoxProfilePath = "",
                SyncFolderPath = "",
                IncludeFolders = new List<string>(),
                ExcludeFolders = new List<string>(),
                ExcludeExtensions = new List<string>()
            };
        }
        private void ToggleDebugConsole(object sender, EventArgs e)
        {
            DebugConsole.Toggle();
        }

        private void LoadConfig()
        {
            DebugConsole.Log($"Loading config from: {configPath}");
            
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonConvert.DeserializeObject<Config>(json);
                    
                    DebugConsole.Log($"Loaded SyncFolderPath: '{config.SyncFolderPath}'");
                    
                    // Se SyncFolderPath for null ou vazio, criar Desktop default
                    if (string.IsNullOrEmpty(config.SyncFolderPath))
                    {
                        DebugConsole.Log("SyncFolderPath is null or empty. Setting default...");
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        config.SyncFolderPath = Path.Combine(desktopPath, APP_NAME);
                        DebugConsole.Log($"Default SyncFolderPath set to: '{config.SyncFolderPath}'");
                        SaveConfig();
                    }
                    else
                    {
                        // Verificar se a pasta existe
                        if (Directory.Exists(config.SyncFolderPath))
                        {
                            DebugConsole.Log($"SyncFolderPath exists: '{config.SyncFolderPath}'");
                        }
                        else
                        {
                            DebugConsole.Log($"SyncFolderPath does NOT exist: '{config.SyncFolderPath}'");
                            // Não sobrescrever automaticamente! Só criar a pasta.
                            try
                            {
                                Directory.CreateDirectory(config.SyncFolderPath);
                                DebugConsole.Log($"Created missing SyncFolder: '{config.SyncFolderPath}'");
                            }
                            catch (Exception ex)
                            {
                                DebugConsole.Log($"Failed to create SyncFolder: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Error loading config: {ex.Message}");
            }

            if (config == null)
            {
                DebugConsole.Log("Config is null. Creating default config...");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                config = new Config
                {
                    BackupIntervalHours = 24,
                    MaxBackups = 10,
                    FirefoxProfilePath = FindFirefoxProfile(),
                    SyncFolderPath = Path.Combine(desktopPath, APP_NAME),
                    IncludeFolders = IncludeFolders.ToList(),
                    ExcludeFolders = ExcludeFolders.ToList(),
                    ExcludeExtensions = ExcludeExtensions.ToList()
                };
                DebugConsole.Log($"Default SyncFolderPath set to: '{config.SyncFolderPath}'");
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);
            DebugConsole.Log($"Saved config. SyncFolderPath: '{config.SyncFolderPath}'");
        }

        public void SaveConfigChanges()
        {
            SaveConfig();
        }

        public void RefreshConfig()
        {
            DebugConsole.Log("RefreshConfig() called. Reloading config...");
            LoadConfig();
        }

        private string FindFirefoxProfile()
        {
            string profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox", "Profiles");
            string firefoxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox");

            string profilesIni = Path.Combine(firefoxPath, "profiles.ini");
            if (File.Exists(profilesIni))
            {
                try
                {
                    string defaultProfilePath = ParseProfilesIni(profilesIni, profilesPath);
                    if (!string.IsNullOrEmpty(defaultProfilePath) && Directory.Exists(defaultProfilePath))
                        return defaultProfilePath;
                }
                catch { }
            }

            if (Directory.Exists(profilesPath))
            {
                string[] releaseProfiles = Directory.GetDirectories(profilesPath, "*.default-release*");
                if (releaseProfiles.Length > 0)
                    return releaseProfiles[0];

                string[] defaultProfiles = Directory.GetDirectories(profilesPath, "*.default*");
                if (defaultProfiles.Length > 0)
                    return defaultProfiles[0];
            }

            return Path.Combine(profilesPath, "default-release");
        }

        public string ParseProfilesIni(string iniPath, string profilesPath)
        {
            string currentSection = null;
            string currentPath = null;
            bool currentIsDefault = false;

            foreach (var line in File.ReadAllLines(iniPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("["))
                {
                    if (currentSection != null && currentIsDefault && currentPath != null)
                    {
                        string fullPath = currentPath.Contains(":\\") || currentPath.StartsWith("/")
                            ? currentPath
                            : Path.Combine(profilesPath, currentPath);
                        if (Directory.Exists(fullPath))
                            return fullPath;
                    }
                    currentSection = trimmed;
                    currentPath = null;
                    currentIsDefault = false;
                }
                else if (trimmed.StartsWith("Path="))
                {
                    currentPath = trimmed.Substring(5);
                }
                else if (trimmed.StartsWith("Default=1"))
                {
                    currentIsDefault = true;
                }
            }

            if (currentIsDefault && currentPath != null)
            {
                string fullPath = currentPath.Contains(":\\") || currentPath.StartsWith("/")
                    ? currentPath
                    : Path.Combine(profilesPath, currentPath);
                if (Directory.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private Icon GenerateIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firekeeper.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch { }

            int size = 64;
            Bitmap bmp = new Bitmap(size, size);
            Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, size, size);
            using (LinearGradientBrush brush = new LinearGradientBrush(rect,
                Color.FromArgb(255, 80, 20),
                Color.FromArgb(255, 160, 20),
                45f))
            {
                g.FillRoundedRectangle(brush, rect, 8);
            }

            using (Pen pen = new Pen(Color.FromArgb(255, 200, 100, 20), 2))
            {
                g.DrawRoundedRectangle(pen, rect, 8);
            }

            Point[] flamePoints = new Point[]
            {
                new Point(32, 12),
                new Point(22, 28),
                new Point(28, 28),
                new Point(20, 44),
                new Point(32, 36),
                new Point(44, 44),
                new Point(36, 28),
                new Point(42, 28)
            };
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(flamePoints);
                using (LinearGradientBrush flameBrush = new LinearGradientBrush(
                    new Rectangle(20, 12, 24, 32),
                    Color.FromArgb(255, 255, 200, 50),
                    Color.FromArgb(255, 255, 100, 0),
                    90f))
                {
                    g.FillPath(flameBrush, path);
                }
            }

            using (Font font = new Font("Segoe UI", 24, FontStyle.Bold))
            {
                string text = "FK";
                SizeF textSize = g.MeasureString(text, font);
                float x = (size - textSize.Width) / 2;
                float y = (size - textSize.Height) / 2 + 4;

                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
                {
                    g.DrawString(text, font, shadowBrush, x + 1, y + 1);
                }

                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(text, font, textBrush, x, y);
                }
            }

            g.Dispose();
            Icon icon = Icon.FromHandle(bmp.GetHicon());
            Bitmap copy = new Bitmap(bmp);
            icon = Icon.FromHandle(copy.GetHicon());
            return icon;
        }
    }
}
