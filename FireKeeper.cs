// FireKeeper.cs - Firefox backup utility
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace FireKeeper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BackupTrayContext());
        }
    }

    public class BackupTrayContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer backupTimer;
        private Config config;
        private string configPath;
        private bool isBackupRunning = false;
        private BackupManagerForm managerForm;
        private const string APP_NAME = "FireKeeper";
        public event Action<int, string> ProgressUpdate;

        private void ReportProgress(int percent, string status)
        {
            ProgressUpdate?.Invoke(percent, status);
        }

        private bool ShouldShowNotifications()
        {
            return managerForm == null || managerForm.IsDisposed;
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

            // Double-click opens the manager
            trayIcon.DoubleClick += (s, e) => OpenManager(s, e);

            // Start backup scheduler
            StartScheduler();

            // Show startup notification after a short delay
            ShowStartupNotification();
        }

        private void ToggleDebugConsole(object sender, EventArgs e)
        {
            DebugConsole.Toggle();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonConvert.DeserializeObject<Config>(json);
                    
                    if (string.IsNullOrEmpty(config.SyncFolderPath))
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        config.SyncFolderPath = Path.Combine(desktopPath, APP_NAME);
                        SaveConfig();
                    }
                }
            }
            catch { }

            if (config == null)
            {
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
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }

        public void SaveConfigChanges()
        {
            SaveConfig();
        }

        public void RefreshConfig()
        {
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

        private string ParseProfilesIni(string iniPath, string profilesPath)
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

        private void StartScheduler()
        {
            backupTimer = new System.Windows.Forms.Timer();
            backupTimer.Interval = 60000;
            backupTimer.Tick += (s, e) => CheckAndBackup();
            backupTimer.Start();
        }

        private async void CheckAndBackup()
        {
            if (isBackupRunning) return;

            string lastBackup = config.LastBackup;
            if (!string.IsNullOrEmpty(lastBackup))
            {
                DateTime lastTime = DateTime.ParseExact(lastBackup, "yyyyMMdd_HHmmss", null);
                TimeSpan diff = DateTime.Now - lastTime;
                if (diff.TotalHours < config.BackupIntervalHours)
                    return;
            }

            await PerformBackup();
        }

        private string GetSyncFolder()
        {
            if (!string.IsNullOrEmpty(config.SyncFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(config.SyncFolderPath);
                    return config.SyncFolderPath;
                }
                catch
                {
                    DebugConsole.Log($"Failed to access sync folder: {config.SyncFolderPath}. Falling back to Desktop.");
                }
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string fallbackDir = Path.Combine(desktopPath, APP_NAME);
            
            try
            {
                Directory.CreateDirectory(fallbackDir);
                DebugConsole.Log($"Using fallback sync folder: {fallbackDir}");
                return fallbackDir;
            }
            catch
            {
                string ultimateFallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    APP_NAME, "backups");
                Directory.CreateDirectory(ultimateFallback);
                DebugConsole.Log($"Using ultimate fallback sync folder: {ultimateFallback}");
                return ultimateFallback;
            }
        }

        private async Task PerformBackup()
        {
            if (isBackupRunning) return;
            isBackupRunning = true;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupName = $"firekeeper_backup_{timestamp}";
                string zipPath = Path.Combine(GetSyncFolder(), $"{backupName}.zip");

                await Task.Run(() => CreateBackupZip(config.FirefoxProfilePath, zipPath));

                config.LastBackup = timestamp;
                SaveConfig();

                CleanOldBackups();

                if (ShouldShowNotifications())
                {
                    trayIcon.ShowBalloonTip(3000, APP_NAME,
                        $"✅ Backup completed successfully at {DateTime.Now:HH:mm}",
                        ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Backup failed: {ex.Message}");
                if (ShouldShowNotifications())
                {
                    trayIcon.ShowBalloonTip(3000, APP_NAME,
                        $"❌ Backup failed: {ex.Message}"+
                        "Click here to open the manager",
                        ToolTipIcon.Error);
                }
            }
            finally
            {
                isBackupRunning = false;
            }
        }

        private void CreateBackupZip(string sourceDir, string destZip)
        {
            DebugConsole.Log($"=== CREATE BACKUP ZIP STARTED ===");
            DebugConsole.Log($"Source directory: {sourceDir}");
            DebugConsole.Log($"Destination zip: {destZip}");
            
            if (!Directory.Exists(sourceDir))
            {
                DebugConsole.Log($"ERROR: Source directory does not exist: {sourceDir}");
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            DebugConsole.Log($"Temp directory: {tempDir}");
            
            try
            {
                Directory.CreateDirectory(tempDir);
                DebugConsole.Log("Temp directory created successfully.");

                HashSet<string> includeFolders;
                HashSet<string> excludeFolders;
                HashSet<string> excludeExtensions;

                if (config.IncludeFolders != null && config.IncludeFolders.Count > 0)
                {
                    includeFolders = new HashSet<string>(config.IncludeFolders);
                    DebugConsole.Log($"Using custom include folders from config ({includeFolders.Count} items)");
                }
                else
                {
                    includeFolders = new HashSet<string>(IncludeFolders);
                    DebugConsole.Log($"Using default include folders ({includeFolders.Count} items)");
                }

                if (config.ExcludeFolders != null && config.ExcludeFolders.Count > 0)
                {
                    excludeFolders = new HashSet<string>(config.ExcludeFolders);
                    DebugConsole.Log($"Using custom exclude folders from config ({excludeFolders.Count} items)");
                }
                else
                {
                    excludeFolders = new HashSet<string>(ExcludeFolders);
                    DebugConsole.Log($"Using default exclude folders ({excludeFolders.Count} items)");
                }

                if (config.ExcludeExtensions != null && config.ExcludeExtensions.Count > 0)
                {
                    excludeExtensions = new HashSet<string>(config.ExcludeExtensions);
                    DebugConsole.Log($"Using custom exclude extensions from config ({excludeExtensions.Count} items)");
                }
                else
                {
                    excludeExtensions = new HashSet<string>(ExcludeExtensions);
                    DebugConsole.Log($"Using default exclude extensions ({excludeExtensions.Count} items)");
                }

                DebugConsole.Log("Scanning for files...");
                string[] allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                int totalFiles = allFiles.Length;
                int processed = 0;
                int copied = 0;
                int skipped = 0;

                DebugConsole.Log($"Found {totalFiles} files in source directory.");

                ReportProgress(0, $"Scanning {totalFiles} files...");

                foreach (string file in allFiles)
                {
                    string relPath = GetRelativePath(sourceDir, file);
                    processed++;
                    
                    if (ShouldSkipFile(relPath, excludeFolders, excludeExtensions))
                    {
                        skipped++;
                        if (skipped <= 5 || skipped % 100 == 0)
                        {
                            DebugConsole.Log($"SKIP: {relPath} (excluded by rules)");
                        }
                        continue;
                    }

                    if (!ShouldIncludeFile(relPath, includeFolders))
                    {
                        skipped++;
                        if (skipped <= 5 || skipped % 100 == 0)
                        {
                            DebugConsole.Log($"SKIP: {relPath} (not in include list)");
                        }
                        continue;
                    }

                    string destFile = Path.Combine(tempDir, relPath);
                    string destDir = Path.GetDirectoryName(destFile);
                    
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                        DebugConsole.Log($"Created directory: {relPath}");
                    }

                    try
                    {
                        File.Copy(file, destFile);
                        copied++;
                        
                        if (copied % 25 == 0)
                        {
                            DebugConsole.Log($"Copied {copied}/{totalFiles}: {relPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.Log($"ERROR copying {relPath}: {ex.Message}");
                        throw;
                    }

                    if (processed % 10 == 0)
                    {
                        int percent = (int)((double)processed / totalFiles * 100);
                        ReportProgress(percent, $"Copying: {Path.GetFileName(file)} ({processed}/{totalFiles})");
                    }
                }

                DebugConsole.Log($"File processing complete: {copied} copied, {skipped} skipped, {totalFiles} total.");
                DebugConsole.Log($"Compressing {copied} files into zip...");

                ReportProgress(90, "Compressing files...");
                
                try
                {
                    ZipFile.CreateFromDirectory(tempDir, destZip);
                    DebugConsole.Log($"Zip created successfully: {destZip}");
                    
                    if (File.Exists(destZip))
                    {
                        var fileInfo = new FileInfo(destZip);
                        DebugConsole.Log($"Zip size: {fileInfo.Length / 1024 / 1024:F2} MB");
                        DebugConsole.Log($"Zip created at: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        DebugConsole.Log($"ERROR: Zip file was not created at {destZip}");
                    }
                }
                catch (Exception ex)
                {
                    DebugConsole.Log($"ERROR creating zip: {ex.Message}");
                    DebugConsole.Log($"Stack trace: {ex.StackTrace}");
                    throw;
                }

                ReportProgress(100, "Backup complete!");
                DebugConsole.Log("=== CREATE BACKUP ZIP COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"=== CREATE BACKUP ZIP FAILED ===");
                DebugConsole.Log($"Exception: {ex.GetType().Name}: {ex.Message}");
                DebugConsole.Log($"Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    DebugConsole.Log($"Cleaning up temp directory: {tempDir}");
                    try
                    {
                        Directory.Delete(tempDir, true);
                        DebugConsole.Log("Temp directory deleted successfully.");
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.Log($"Failed to delete temp directory: {ex.Message}");
                    }
                }
            }
        }

        private bool ShouldIncludeFile(string relPath, HashSet<string> includeFolders)
        {
            if (includeFolders == null || includeFolders.Count == 0)
                return true;

            string[] parts = relPath.Split('\\');

            foreach (string part in parts)
            {
                if (includeFolders.Contains(part))
                    return true;
            }

            foreach (string folder in includeFolders)
            {
                if (relPath.StartsWith(folder + "\\") || relPath == folder)
                    return true;
            }

            if (parts.Length == 1)
            {
                string fileName = parts[0];
                string[] importantRootFiles = new[]
                {
                    "prefs.js", "places.sqlite", "cookies.sqlite", "logins.json",
                    "key3.db", "key4.db", "xulstore.json", "handlers.json",
                    "containers.json", "permissions.sqlite", "favicons.sqlite",
                    "formhistory.sqlite", "search.json.mozlz4", "sessionstore.jsonlz4",
                    "storage-sync-v2.sqlite"
                };
                return importantRootFiles.Contains(fileName);
            }

            return false;
        }

        private bool ShouldSkipFile(string relPath, HashSet<string> excludeFolders, HashSet<string> excludeExtensions)
        {
            if (relPath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                return true;

            if (excludeExtensions != null)
            {
                string ext = Path.GetExtension(relPath);
                if (!string.IsNullOrEmpty(ext) && excludeExtensions.Contains(ext.ToLowerInvariant()))
                    return true;
            }

            if (excludeFolders != null)
            {
                string[] parts = relPath.Split('\\');
                foreach (string part in parts)
                {
                    if (excludeFolders.Contains(part))
                        return true;
                }

                foreach (string folder in excludeFolders)
                {
                    if (relPath.StartsWith(folder + "\\") || relPath == folder)
                        return true;
                }
            }

            return false;
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            if (!basePath.EndsWith("\\")) basePath += "\\";
            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', '\\'));
        }

        private void CleanOldBackups()
        {
            string syncFolder = GetSyncFolder();
            try
            {
                if (!Directory.Exists(syncFolder))
                    return;

                DebugConsole.Log($"Cleaning sync folder: {syncFolder} (max {config.MaxBackups} backups)");

                var backupFiles = Directory.GetFiles(syncFolder, "firekeeper_backup_*.zip")
                    .OrderBy(f => f)
                    .ToList();

                DebugConsole.Log($"Found {backupFiles.Count} backups in sync folder");

                while (backupFiles.Count > config.MaxBackups)
                {
                    string fileToDelete = backupFiles[0];
                    DebugConsole.Log($"Deleting old backup: {Path.GetFileName(fileToDelete)}");
                    try
                    {
                        File.Delete(fileToDelete);
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.Log($"Failed to delete {fileToDelete}: {ex.Message}");
                    }
                    backupFiles.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Error cleaning sync folder: {ex.Message}");
            }
        }

        private void OpenManager(object sender, EventArgs e)
        {
            if (managerForm == null || managerForm.IsDisposed)
            {
                managerForm = new BackupManagerForm(config, this);
                
                // Subscribe to progress updates
                ProgressUpdate += managerForm.UpdateProgress;
                
                // Clean up reference when form is closed
                managerForm.FormClosed += (s, args) =>
                {
                    ProgressUpdate -= managerForm.UpdateProgress;
                    managerForm = null;
                };
            }
            
            managerForm.Show();
            managerForm.BringToFront();
        }

        public void ManualBackup(object sender, EventArgs e)
        {
            _ = Task.Run(() => PerformBackup());
        }

        public async Task ManualBackupAsync()
        {
            await PerformBackup();
        }

        private void Exit(object sender, EventArgs e)
        {
            try
            {
                // Check if manager is open
                if (managerForm != null && !managerForm.IsDisposed && managerForm.Visible)
                {
                    var result = MessageBox.Show(
                        "The Backup Manager window is still open.\n\n" +
                        "Do you want to close it and exit FireKeeper?",
                        APP_NAME,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.No)
                        return;
                }

                // Close manager
                if (managerForm != null && !managerForm.IsDisposed)
                {
                    managerForm.Close();
                    managerForm.Dispose();
                    managerForm = null;
                }

                // Clean up tray - with null check
                if (trayIcon != null)
                {
                    try
                    {
                        trayIcon.Visible = false;
                        trayIcon.Dispose();
                    }
                    catch { }
                    trayIcon = null;
                }

                // Clean up timer - with null check
                if (backupTimer != null)
                {
                    try
                    {
                        backupTimer.Stop();
                        backupTimer.Dispose();
                    }
                    catch { }
                    backupTimer = null;
                }

                // Exit the application
                Application.Exit();
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Error during exit: {ex.Message}");
                // Force exit if something goes wrong
                Environment.Exit(0);
            }
        }
        public string ShowProfileSelector(string title)
        {
            string profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox", "Profiles");

            var profiles = new List<(string Path, string Name)>();

            if (Directory.Exists(profilesPath))
            {
                foreach (var dir in Directory.GetDirectories(profilesPath, "*.default*"))
                {
                    string name = Path.GetFileName(dir);
                    profiles.Add((dir, name));
                }
            }

            if (profiles.Count == 0)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = title;
                    fbd.SelectedPath = profilesPath;
                    fbd.ShowNewFolderButton = false;
                    return fbd.ShowDialog() == DialogResult.OK ? fbd.SelectedPath : null;
                }
            }

            if (profiles.Count == 1)
            {
                return profiles[0].Path;
            }

            using (Form selector = new Form())
            {
                selector.Text = title;
                selector.Size = new System.Drawing.Size(500, 300);
                selector.StartPosition = FormStartPosition.CenterScreen;
                selector.FormBorderStyle = FormBorderStyle.FixedDialog;
                selector.MaximizeBox = false;
                selector.MinimizeBox = false;
                try { selector.Icon = this.trayIcon.Icon; } catch { }

                TableLayoutPanel panel = new TableLayoutPanel();
                panel.Dock = DockStyle.Fill;
                panel.Padding = new Padding(20);
                panel.RowCount = 3;

                Label header = new Label();
                header.Text = "🔥 FireKeeper detected multiple Firefox profiles:\n" +
                              "Select the one you want to use:";
                header.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                header.AutoSize = true;
                panel.Controls.Add(header, 0, 0);

                ListBox listBox = new ListBox();
                listBox.Dock = DockStyle.Fill;
                listBox.Font = new Font("Segoe UI", 10);
                foreach (var p in profiles)
                {
                    listBox.Items.Add($"{p.Name}  —  {p.Path}");
                }
                listBox.SelectedIndex = 0;
                panel.Controls.Add(listBox, 0, 1);

                FlowLayoutPanel btnPanel = new FlowLayoutPanel();
                btnPanel.FlowDirection = FlowDirection.RightToLeft;
                btnPanel.Dock = DockStyle.Bottom;
                btnPanel.Height = 40;

                Button okBtn = new Button();
                okBtn.Text = "Select";
                okBtn.Size = new System.Drawing.Size(100, 32);
                okBtn.DialogResult = DialogResult.OK;
                okBtn.Click += (s, e) => selector.DialogResult = DialogResult.OK;
                btnPanel.Controls.Add(okBtn);

                Button cancelBtn = new Button();
                cancelBtn.Text = "Cancel";
                cancelBtn.Size = new System.Drawing.Size(100, 32);
                cancelBtn.DialogResult = DialogResult.Cancel;
                btnPanel.Controls.Add(cancelBtn);

                panel.Controls.Add(btnPanel, 0, 2);
                selector.Controls.Add(panel);
                selector.AcceptButton = okBtn;
                selector.CancelButton = cancelBtn;

                DialogResult result = selector.ShowDialog();
                if (result == DialogResult.OK && listBox.SelectedIndex >= 0)
                {
                    return profiles[listBox.SelectedIndex].Path;
                }
                return null;
            }
        }

        public async Task RestoreBackup(string zipPath)
        {
            DebugConsole.Log("=== RESTORE STARTED ===");
            ReportProgress(0, "Starting restore...");

            if (IsFirefoxRunning())
            {
                DebugConsole.Log("ERROR: Firefox is running");
                ReportProgress(0, "❌ Firefox is running!");
                MessageBox.Show(
                    "Firefox is currently running.\n\n" +
                    "Please close Firefox completely before restoring. " +
                    "Check Task Manager for any remaining firefox.exe processes.",
                    APP_NAME,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string profilePath = ShowProfileSelector("Select the Firefox profile to restore to:");
                if (string.IsNullOrEmpty(profilePath))
                {
                    ReportProgress(0, "❌ Restore cancelled");
                    return;
                }

                if (!Directory.Exists(profilePath))
                {
                    MessageBox.Show($"Profile folder not found: {profilePath}", APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ReportProgress(0, "❌ Profile not found");
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"WARNING: This will overwrite your current Firefox profile!\n\n" +
                    $"Profile: {profilePath}\n" +
                    $"Backup: {Path.GetFileName(zipPath)}\n\n" +
                    $"Are you sure you want to continue?",
                    APP_NAME,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult == DialogResult.No)
                {
                    ReportProgress(0, "❌ Restore cancelled");
                    return;
                }

                ReportProgress(5, "Creating pre-restore backup...");
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string preRestoreBackup = Path.Combine(GetSyncFolder(), $"pre_restore_backup_{timestamp}.zip");
                await Task.Run(() => CreateBackupZip(profilePath, preRestoreBackup));

                ReportProgress(15, "Extracting backup...");
                string tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    Directory.CreateDirectory(tempExtractDir);
                    ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                    string extractRoot = tempExtractDir;
                    var subDirs = Directory.GetDirectories(tempExtractDir);
                    var rootFiles = Directory.GetFiles(tempExtractDir);
                    if (subDirs.Length == 1 && rootFiles.Length == 0)
                        extractRoot = subDirs[0];

                    var allFiles = Directory.GetFiles(extractRoot, "*.*", SearchOption.AllDirectories);
                    int totalFiles = allFiles.Length;
                    int restored = 0;

                    ReportProgress(20, $"Clearing profile ({totalFiles} files to restore)...");
                    ClearProfileDirectory(profilePath);

                    var failedFiles = new List<string>();
                    int processed = 0;

                    foreach (string file in allFiles)
                    {
                        string relPath = GetRelativePath(extractRoot, file);
                        string destFile = Path.Combine(profilePath, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                        bool copied = TryCopyFileWithRetry(file, destFile, 3, 1000);
                        if (copied)
                            restored++;
                        else
                            failedFiles.Add(relPath);

                        processed++;
                        if (processed % 5 == 0)
                        {
                            int percent = 20 + (int)((double)processed / totalFiles * 70);
                            ReportProgress(percent, $"Restoring: {Path.GetFileName(file)} ({processed}/{totalFiles})");
                        }
                    }

                    ReportProgress(95, "Finalizing...");
                    string resultMessage = BuildRestoreResultMessage(profilePath, preRestoreBackup, restored, failedFiles);
                    MessageBox.Show(resultMessage, APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ReportProgress(100, "✅ Restore complete!");
                }
                finally
                {
                    if (Directory.Exists(tempExtractDir))
                        Directory.Delete(tempExtractDir, true);
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"EXCEPTION: {ex.Message}");
                MessageBox.Show($"Restore failed: {ex.Message}",
                    APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ReportProgress(0, "❌ Restore failed");
            }
        }

        private void ClearProfileDirectory(string profilePath)
        {
            DebugConsole.Log($"ClearProfileDirectory called for: {profilePath}");
            var dirInfo = new DirectoryInfo(profilePath);

            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            DebugConsole.Log($"Found {files.Length} files to delete.");
            foreach (var file in files)
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                }
                catch (Exception ex)
                {
                    DebugConsole.Log($"  Failed to delete file {file.FullName}: {ex.Message}");
                }
            }

            var dirs = dirInfo.GetDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length).ToArray();
            DebugConsole.Log($"Found {dirs.Length} directories to delete.");
            foreach (var dir in dirs)
            {
                try
                {
                    dir.Attributes = FileAttributes.Normal;
                    dir.Delete(true);
                }
                catch (Exception ex)
                {
                    DebugConsole.Log($"  Failed to delete dir {dir.FullName}: {ex.Message}");
                }
            }
            DebugConsole.Log("ClearProfileDirectory finished.");
        }

        private bool TryCopyFileWithRetry(string source, string destination, int maxAttempts, int delayMs)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    File.Copy(source, destination, overwrite: true);
                    return true;
                }
                catch (IOException ex)
                {
                    DebugConsole.Log($"  Copy attempt {attempt + 1} failed for {Path.GetFileName(source)}: {ex.Message}");
                    if (attempt < maxAttempts - 1)
                        System.Threading.Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    DebugConsole.Log($"  Non-retryable error copying {Path.GetFileName(source)}: {ex.Message}");
                    break;
                }
            }
            return false;
        }

        private string BuildRestoreResultMessage(
            string profilePath, string preRestoreBackup, int restoredCount, List<string> failedFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("✅ Backup restored successfully!");
            sb.AppendLine();
            sb.AppendLine($"Profile: {profilePath}");
            sb.AppendLine($"Files restored: {restoredCount}");
            sb.AppendLine($"Pre-restore backup: {preRestoreBackup}");
            sb.AppendLine();

            if (failedFiles.Count > 0)
            {
                sb.AppendLine("⚠️ Files that could not be restored:");
                foreach (var f in failedFiles.Take(10))
                    sb.AppendLine($"  - {f}");
                if (failedFiles.Count > 10)
                    sb.AppendLine($"  ... and {failedFiles.Count - 10} more");
                sb.AppendLine();
            }

            sb.AppendLine("Restart Firefox. If tabs don't appear:");
            sb.AppendLine("  → History → Restore Previous Session");
            sb.AppendLine("  OR Settings → General → Startup → Restore previous session");

            return sb.ToString();
        }

        private bool IsFirefoxRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("firefox");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void ShowStartupNotification()
        {
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 500;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();

                string nextBackup = "soon";
                if (!string.IsNullOrEmpty(config.LastBackup))
                {
                    try
                    {
                        DateTime lastTime = DateTime.ParseExact(config.LastBackup, "yyyyMMdd_HHmmss", null);
                        DateTime nextTime = lastTime.AddHours(config.BackupIntervalHours);
                        nextBackup = nextTime.ToString("HH:mm");
                    }
                    catch { }
                }

                string message = $"🚀 {APP_NAME} now running.\n" +
                                 $"Click here to open the manager\n" +
                                 $"⏰ Next backup: ~{nextBackup}\n" +
                                 $"💾 Sync folder: {GetSyncFolder()}";

                trayIcon.ShowBalloonTip(4000, APP_NAME, message, ToolTipIcon.Info);
            };
            timer.Start();
        }
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

    public static class DebugConsole
    {
        private static Form _form;
        private static TextBox _textBox;
        private static readonly object _lock = new object();
        private static bool _isVisible = false;

        public static void Show()
        {
            lock (_lock)
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.Show();
                    _form.BringToFront();
                    _isVisible = true;
                    return;
                }

                _form = new Form
                {
                    Text = "FireKeeper Debug Console",
                    Size = new System.Drawing.Size(900, 600),
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(50, 50),
                    FormBorderStyle = FormBorderStyle.Sizable
                };

                _textBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10),
                    BackColor = Color.Black,
                    ForeColor = Color.LimeGreen,
                    ReadOnly = true
                };

                _form.Controls.Add(_textBox);
                _form.FormClosing += (s, e) =>
                {
                    _isVisible = false;
                    e.Cancel = true;
                    _form.Hide();
                };
                _form.Show();
                _isVisible = true;
                Log("Debug console opened.");
            }
        }

        public static void Hide()
        {
            lock (_lock)
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.Hide();
                    _isVisible = false;
                }
            }
        }

        public static void Toggle()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                
                if (_form != null && !_form.IsDisposed && _textBox != null && !_textBox.IsDisposed)
                {
                    if (_textBox.InvokeRequired)
                    {
                        _textBox.Invoke(new Action(() =>
                        {
                            _textBox.AppendText(line + Environment.NewLine);
                            _textBox.SelectionStart = _textBox.Text.Length;
                            _textBox.ScrollToCaret();
                        }));
                    }
                    else
                    {
                        _textBox.AppendText(line + Environment.NewLine);
                        _textBox.SelectionStart = _textBox.Text.Length;
                        _textBox.ScrollToCaret();
                    }
                }
                
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "FireKeeper", "debug.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, line + Environment.NewLine);
                }
                catch { }
            }
        }
    }

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (GraphicsPath path = GetRoundedRectanglePath(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using (GraphicsPath path = GetRoundedRectanglePath(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class BackupManagerForm : Form
    {
        private Config config;
        private BackupTrayContext context;
        private System.Windows.Forms.TextBox profilePathBox;
        private System.Windows.Forms.TextBox syncFolderBox;
        private System.Windows.Forms.NumericUpDown intervalBox;
        private System.Windows.Forms.NumericUpDown maxBackupsBox;
        private ProgressBar progressBar;
        private Label progressLabel;
        private Label statusLabel;
        private Button backupNowBtn;
        private Button restoreBtn;
        private Button saveBtn;
        private const string APP_NAME = "FireKeeper";

        public BackupManagerForm(Config cfg, BackupTrayContext ctx)
        {
            config = cfg;
            context = ctx;
            InitializeComponents();
        }

        public void UpdateProgress(int percent, string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateProgress(percent, status)));
                return;
            }

            progressBar.Value = Math.Min(100, Math.Max(0, percent));
            progressLabel.Text = status;
            statusLabel.Text = status;

            if (percent >= 100)
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                statusLabel.Text = "✅ " + status;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
        }

        public void ResetProgress()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ResetProgress));
                return;
            }

            progressBar.Value = 0;
            progressLabel.Text = "Ready";
            statusLabel.Text = "✅ Ready";
            progressBar.Style = ProgressBarStyle.Blocks;
        }

        private void InitializeComponents()
        {
            this.Text = APP_NAME + " - Backup Manager";
            this.Size = new System.Drawing.Size(580, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firekeeper.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
                else
                    this.Icon = GetDefaultIcon();
            }
            catch
            {
                this.Icon = GetDefaultIcon();
            }

            TableLayoutPanel mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Padding = new Padding(20);
            mainPanel.RowCount = 14;
            mainPanel.ColumnCount = 2;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            FlowLayoutPanel titlePanel = new FlowLayoutPanel();
            titlePanel.FlowDirection = FlowDirection.LeftToRight;
            titlePanel.AutoSize = true;

            Label titleLabel = new Label();
            titleLabel.Text = "🔥 " + APP_NAME;
            titleLabel.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(200, 80, 20);
            titleLabel.AutoSize = true;
            titlePanel.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = " - Firefox Backup";
            subtitleLabel.Font = new Font("Segoe UI", 12);
            subtitleLabel.ForeColor = Color.Gray;
            subtitleLabel.AutoSize = true;
            titlePanel.Controls.Add(subtitleLabel);

            mainPanel.Controls.Add(titlePanel, 0, 0);
            mainPanel.SetColumnSpan(titlePanel, 2);

            mainPanel.Controls.Add(new Label() { Text = "", Height = 2, BackColor = Color.LightGray }, 0, 1);
            mainPanel.SetColumnSpan(new Label() { Text = "" }, 2);

            Label profileLabel = new Label();
            profileLabel.Text = "📁 Firefox Profile:";
            profileLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            profileLabel.AutoSize = true;
            mainPanel.Controls.Add(profileLabel, 0, 2);

            FlowLayoutPanel profilePanel = new FlowLayoutPanel();
            profilePanel.FlowDirection = FlowDirection.LeftToRight;
            profilePanel.AutoSize = true;

            profilePathBox = new System.Windows.Forms.TextBox();
            profilePathBox.Text = config.FirefoxProfilePath;
            profilePathBox.Width = 320;
            profilePathBox.ReadOnly = true;
            profilePathBox.Font = new Font("Segoe UI", 9);
            profilePanel.Controls.Add(profilePathBox);

            Button browseProfileBtn = new Button();
            browseProfileBtn.Text = "📂 Select...";
            browseProfileBtn.AutoSize = true;
            browseProfileBtn.Font = new Font("Segoe UI", 9);
            browseProfileBtn.Click += (s, e) =>
            {
                string selected = context.ShowProfileSelector("Select the Firefox profile to back up:");
                if (!string.IsNullOrEmpty(selected))
                {
                    profilePathBox.Text = selected;
                }
            };
            profilePanel.Controls.Add(browseProfileBtn);

            mainPanel.Controls.Add(profilePanel, 1, 2);

            Label folderLabel = new Label();
            folderLabel.Text = "☁️ Sync Folder:";
            folderLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            folderLabel.AutoSize = true;
            mainPanel.Controls.Add(folderLabel, 0, 3);

            FlowLayoutPanel folderPanel = new FlowLayoutPanel();
            folderPanel.FlowDirection = FlowDirection.LeftToRight;
            folderPanel.AutoSize = true;

            syncFolderBox = new System.Windows.Forms.TextBox();
            syncFolderBox.Text = GetDefaultSyncFolder();
            syncFolderBox.Width = 320;
            syncFolderBox.ReadOnly = true;
            syncFolderBox.Font = new Font("Segoe UI", 9);
            folderPanel.Controls.Add(syncFolderBox);

            Button browseFolderBtn = new Button();
            browseFolderBtn.Text = "📂 Browse...";
            browseFolderBtn.AutoSize = true;
            browseFolderBtn.Font = new Font("Segoe UI", 9);
            browseFolderBtn.Click += (s, e) =>
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the folder where backups will be stored " +
                        "(e.g. Google Drive, Dropbox, OneDrive, or any local folder):";
                    dialog.SelectedPath = syncFolderBox.Text;
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        syncFolderBox.Text = dialog.SelectedPath;
                    }
                }
            };
            folderPanel.Controls.Add(browseFolderBtn);

            mainPanel.Controls.Add(folderPanel, 1, 3);

            mainPanel.Controls.Add(new Label() { Text = "", Height = 2, BackColor = Color.LightGray }, 0, 4);
            mainPanel.SetColumnSpan(new Label() { Text = "" }, 2);

            Label settingsLabel = new Label();
            settingsLabel.Text = "⚙️ Backup Settings:";
            settingsLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            settingsLabel.AutoSize = true;
            mainPanel.Controls.Add(settingsLabel, 0, 5);
            mainPanel.SetColumnSpan(settingsLabel, 2);

            FlowLayoutPanel intervalPanel = new FlowLayoutPanel();
            intervalPanel.FlowDirection = FlowDirection.LeftToRight;
            intervalPanel.AutoSize = true;
            intervalPanel.Controls.Add(new Label() { Text = "Interval (hours):", AutoSize = true, Width = 110, Font = new Font("Segoe UI", 9) });
            intervalBox = new System.Windows.Forms.NumericUpDown();
            intervalBox.Value = config.BackupIntervalHours;
            intervalBox.Minimum = 1;
            intervalBox.Maximum = 168;
            intervalBox.Width = 60;
            intervalBox.Font = new Font("Segoe UI", 9);
            intervalPanel.Controls.Add(intervalBox);
            mainPanel.Controls.Add(intervalPanel, 0, 6);
            mainPanel.SetColumnSpan(intervalPanel, 2);

            FlowLayoutPanel maxPanel = new FlowLayoutPanel();
            maxPanel.FlowDirection = FlowDirection.LeftToRight;
            maxPanel.AutoSize = true;
            maxPanel.Controls.Add(new Label() { Text = "Max backups to keep:", AutoSize = true, Width = 110, Font = new Font("Segoe UI", 9) });
            maxBackupsBox = new System.Windows.Forms.NumericUpDown();
            maxBackupsBox.Value = config.MaxBackups;
            maxBackupsBox.Minimum = 1;
            maxBackupsBox.Maximum = 50;
            maxBackupsBox.Width = 60;
            maxBackupsBox.Font = new Font("Segoe UI", 9);
            maxPanel.Controls.Add(maxBackupsBox);
            mainPanel.Controls.Add(maxPanel, 0, 7);
            mainPanel.SetColumnSpan(maxPanel, 2);

            mainPanel.Controls.Add(new Label() { Text = "", Height = 2, BackColor = Color.LightGray }, 0, 8);
            mainPanel.SetColumnSpan(new Label() { Text = "" }, 2);

            Label progressTitle = new Label();
            progressTitle.Text = "📊 Progress:";
            progressTitle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            progressTitle.AutoSize = true;
            mainPanel.Controls.Add(progressTitle, 0, 9);
            mainPanel.SetColumnSpan(progressTitle, 2);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Height = 25;
            mainPanel.Controls.Add(progressBar, 0, 10);
            mainPanel.SetColumnSpan(progressBar, 2);

            progressLabel = new Label();
            progressLabel.Text = "Ready";
            progressLabel.Font = new Font("Segoe UI", 9);
            progressLabel.ForeColor = Color.Gray;
            progressLabel.AutoSize = true;
            mainPanel.Controls.Add(progressLabel, 0, 11);
            mainPanel.SetColumnSpan(progressLabel, 2);

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.AutoSize = true;
            buttonPanel.Controls.Add(new Label() { Text = "", Width = 20 });

            saveBtn = new Button();
            saveBtn.Text = "💾 Save Settings";
            saveBtn.Size = new System.Drawing.Size(130, 32);
            saveBtn.Font = new Font("Segoe UI", 9);
            saveBtn.BackColor = Color.FromArgb(76, 175, 80);
            saveBtn.ForeColor = Color.White;
            saveBtn.FlatStyle = FlatStyle.Flat;
            saveBtn.Click += (s, e) =>
            {
                config.BackupIntervalHours = (int)intervalBox.Value;
                config.MaxBackups = (int)maxBackupsBox.Value;
                config.FirefoxProfilePath = profilePathBox.Text;
                config.SyncFolderPath = syncFolderBox.Text;
                
                context.SaveConfigChanges();
                
                MessageBox.Show("✅ Settings saved successfully!", APP_NAME,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            buttonPanel.Controls.Add(saveBtn);

            backupNowBtn = new Button();
            backupNowBtn.Text = "🔄 Backup Now";
            backupNowBtn.Size = new System.Drawing.Size(130, 32);
            backupNowBtn.Font = new Font("Segoe UI", 9);
            backupNowBtn.BackColor = Color.FromArgb(255, 152, 0);
            backupNowBtn.ForeColor = Color.White;
            backupNowBtn.FlatStyle = FlatStyle.Flat;
            backupNowBtn.Click += async (s, e) =>
            {
                backupNowBtn.Enabled = false;
                backupNowBtn.Text = "⏳ Running...";
                progressLabel.Text = "⏳ Creating backup...";
                statusLabel.Text = "⏳ Creating backup...";

                try
                {
                    await context.ManualBackupAsync();
                    MessageBox.Show("✅ Backup completed successfully!", APP_NAME,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = "✅ Backup completed";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Backup failed: {ex.Message}", APP_NAME,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "❌ Backup failed";
                }
                finally
                {
                    backupNowBtn.Enabled = true;
                    backupNowBtn.Text = "🔄 Backup Now";
                    if (statusLabel.Text == "⏳ Creating backup...")
                        statusLabel.Text = "✅ Ready";
                }
            };
            buttonPanel.Controls.Add(backupNowBtn);

            restoreBtn = new Button();
            restoreBtn.Text = "📥 Restore Backup";
            restoreBtn.Size = new System.Drawing.Size(130, 32);
            restoreBtn.Font = new Font("Segoe UI", 9);
            restoreBtn.BackColor = Color.FromArgb(255, 87, 34);
            restoreBtn.ForeColor = Color.White;
            restoreBtn.FlatStyle = FlatStyle.Flat;
            restoreBtn.Click += async (s, e) =>
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select Backup to Restore";
                    dialog.Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*";
                    dialog.InitialDirectory = GetSyncFolder();

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        restoreBtn.Enabled = false;
                        restoreBtn.Text = "⏳ Restoring...";
                        statusLabel.Text = "⏳ Restoring backup...";
                        progressLabel.Text = "⏳ Restoring backup...";

                        try
                        {
                            await context.RestoreBackup(dialog.FileName);
                        }
                        finally
                        {
                            restoreBtn.Enabled = true;
                            restoreBtn.Text = "📥 Restore Backup";
                            statusLabel.Text = "✅ Ready";
                            progressLabel.Text = "Ready";
                        }
                    }
                }
            };
            buttonPanel.Controls.Add(restoreBtn);

            mainPanel.Controls.Add(buttonPanel, 0, 12);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            statusLabel = new Label();
            statusLabel.Text = "✅ Ready";
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            statusLabel.BackColor = Color.FromArgb(240, 240, 240);
            statusLabel.Padding = new Padding(5);
            statusLabel.Font = new Font("Segoe UI", 9);
            mainPanel.Controls.Add(statusLabel, 0, 13);
            mainPanel.SetColumnSpan(statusLabel, 2);
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            this.Controls.Add(mainPanel);
        }

        private string GetDefaultSyncFolder()
        {
            if (!string.IsNullOrEmpty(config.SyncFolderPath))
                return config.SyncFolderPath;

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string defaultDir = Path.Combine(desktopPath, APP_NAME);
            
            try
            {
                Directory.CreateDirectory(defaultDir);
                return defaultDir;
            }
            catch
            {
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    APP_NAME, "backups");
                Directory.CreateDirectory(appDataDir);
                return appDataDir;
            }
        }

        private string GetSyncFolder()
        {
            string dir = !string.IsNullOrEmpty(config.SyncFolderPath)
                ? config.SyncFolderPath
                : GetDefaultSyncFolder();
            return Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private Icon GetDefaultIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(200, 80, 20));
                using (Font font = new Font("Segoe UI", 16, FontStyle.Bold))
                {
                    g.DrawString("FK", font, Brushes.White, 2, 2);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}