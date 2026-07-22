// FireKeeper.cs - Updated backup selection
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private string driveAccessToken;
        private string driveRefreshToken;
        private bool isBackupRunning = false;
        private BackupManagerForm managerForm;
        private HttpListener oauthListener;
        private string oauthState;
        private const string APP_NAME = "FireKeeper";
        private GoogleDriveConfig googleConfig;

        // Backup selection rules
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
            // Load Google credentials from config
            try
            {
                googleConfig = ConfigManager.GetGoogleDriveConfig();
                DebugConsole.SetEnabled(googleConfig.DebugEnabled);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, APP_NAME + " - Setup Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                googleConfig = new GoogleDriveConfig { ClientId = "", ClientSecret = "" };
                DebugConsole.SetEnabled(false);
            }

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

            // Build menu
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open Manager", null, OpenManager);
            menu.Items.Add("Backup Now", null, ManualBackup);
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, Exit);
            trayIcon.ContextMenuStrip = menu;

            // Double-click opens the manager
            trayIcon.DoubleClick += (s, e) => OpenManager(s, e);

            // Start backup scheduler
            StartScheduler();

            // Initialize Google Drive if configured
            if (!string.IsNullOrEmpty(config.DriveRefreshToken))
            {
                driveRefreshToken = config.DriveRefreshToken;
                Task.Run(() => RefreshDriveToken());
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonConvert.DeserializeObject<Config>(json);
                }
            }
            catch { }

            if (config == null)
            {
                config = new Config
                {
                    BackupIntervalHours = 24,
                    MaxBackups = 10,
                    FirefoxProfilePath = FindFirefoxProfile(),
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

        private string FindFirefoxProfile()
        {
            string profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox", "Profiles");
            
            if (Directory.Exists(profilesPath))
            {
                string[] profiles = Directory.GetDirectories(profilesPath, "*.default*");
                if (profiles.Length > 0)
                    return profiles[0];
            }
            return Path.Combine(profilesPath, "default");
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

        private async Task PerformBackup()
        {
            if (isBackupRunning) return;
            isBackupRunning = true;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupName = $"firekeeper_backup_{timestamp}";
                string zipPath = Path.Combine(GetBackupDir(), $"{backupName}.zip");

                await Task.Run(() => CreateBackupZip(config.FirefoxProfilePath, zipPath));

                if (!string.IsNullOrEmpty(driveAccessToken))
                {
                    await UploadToDrive(zipPath, backupName);
                }

                config.LastBackup = timestamp;
                SaveConfig();
                CleanOldBackups();

                trayIcon.ShowBalloonTip(3000, APP_NAME, 
                    $"✅ Backup completed successfully at {DateTime.Now:HH:mm}", 
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                trayIcon.ShowBalloonTip(3000, APP_NAME, 
                    $"❌ Backup failed: {ex.Message}", 
                    ToolTipIcon.Error);
            }
            finally
            {
                isBackupRunning = false;
            }
        }

        // Replace the CreateBackupZip method with this fixed version:

        private void CreateBackupZip(string sourceDir, string destZip)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempDir);

                // Get include and exclude rules from config
                HashSet<string> includeFolders;
                HashSet<string> excludeFolders;
                HashSet<string> excludeExtensions;

                // Convert Lists to HashSets if they exist, otherwise use defaults
                if (config.IncludeFolders != null && config.IncludeFolders.Count > 0)
                    includeFolders = new HashSet<string>(config.IncludeFolders);
                else
                    includeFolders = new HashSet<string>(IncludeFolders);

                if (config.ExcludeFolders != null && config.ExcludeFolders.Count > 0)
                    excludeFolders = new HashSet<string>(config.ExcludeFolders);
                else
                    excludeFolders = new HashSet<string>(ExcludeFolders);

                if (config.ExcludeExtensions != null && config.ExcludeExtensions.Count > 0)
                    excludeExtensions = new HashSet<string>(config.ExcludeExtensions);
                else
                    excludeExtensions = new HashSet<string>(ExcludeExtensions);

                string[] allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                int totalFiles = allFiles.Length;
                int copiedFiles = 0;

                foreach (string file in allFiles)
                {
                    string relPath = GetRelativePath(sourceDir, file);
                    
                    // Skip if file should be excluded
                    if (ShouldSkipFile(relPath, excludeFolders, excludeExtensions))
                        continue;

                    // Check if file is in included folders
                    if (!ShouldIncludeFile(relPath, includeFolders))
                        continue;

                    string destFile = Path.Combine(tempDir, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile);
                    copiedFiles++;
                }

                // Create zip
                ZipFile.CreateFromDirectory(tempDir, destZip);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        private bool ShouldIncludeFile(string relPath, HashSet<string> includeFolders)
        {
            // If no include folders specified, include everything not excluded
            if (includeFolders == null || includeFolders.Count == 0)
                return true;

            string[] parts = relPath.Split('\\');
            
            // Check if any part of the path is in include list
            foreach (string part in parts)
            {
                if (includeFolders.Contains(part))
                    return true;
            }

            // Check if the full path starts with any included folder
            foreach (string folder in includeFolders)
            {
                if (relPath.StartsWith(folder + "\\") || relPath == folder)
                    return true;
            }

            // Also include root-level files (important files like prefs.js, places.sqlite, etc.)
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
            // Skip .lock files
            if (relPath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                return true;

            // Skip files with excluded extensions
            if (excludeExtensions != null)
            {
                string ext = Path.GetExtension(relPath);
                if (!string.IsNullOrEmpty(ext) && excludeExtensions.Contains(ext.ToLowerInvariant()))
                    return true;
            }

            // Skip files in excluded folders
            if (excludeFolders != null)
            {
                string[] parts = relPath.Split('\\');
                foreach (string part in parts)
                {
                    if (excludeFolders.Contains(part))
                        return true;
                }

                // Check if any excluded folder is a prefix of the path
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
            string backupDir = GetBackupDir();
            List<string> backups = Directory.GetFiles(backupDir, "firekeeper_backup_*.zip")
                .OrderBy(f => f)
                .ToList();

            while (backups.Count > config.MaxBackups)
            {
                File.Delete(backups[0]);
                backups.RemoveAt(0);
            }
        }

        private string GetBackupDir()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                APP_NAME, "backups");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private async Task RefreshDriveToken()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    var values = new NameValueCollection();
                    values["client_id"] = googleConfig.ClientId;
                    values["client_secret"] = googleConfig.ClientSecret;
                    values["refresh_token"] = config.DriveRefreshToken;
                    values["grant_type"] = "refresh_token";

                    byte[] response = await client.UploadValuesTaskAsync(
                        "https://oauth2.googleapis.com/token", values);
                    
                    string json = Encoding.UTF8.GetString(response);
                    JObject result = JObject.Parse(json);
                    
                    if (result["access_token"] != null)
                    {
                        driveAccessToken = result["access_token"].ToString();
                    }
                }
            }
            catch { }
        }

        private async Task UploadToDrive(string filePath, string backupName)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Authorization", $"Bearer {driveAccessToken}");

                    string folderId = config.DriveFolderId;
                    if (string.IsNullOrEmpty(folderId))
                    {
                        var folderData = new { 
                            name = APP_NAME + " Backups", 
                            mimeType = "application/vnd.google-apps.folder" 
                        };
                        string folderJson = JsonConvert.SerializeObject(folderData);
                        client.Headers.Add("Content-Type", "application/json");
                        
                        byte[] folderResponse = await client.UploadDataTaskAsync(
                            "https://www.googleapis.com/drive/v3/files", "POST", 
                            Encoding.UTF8.GetBytes(folderJson));
                        
                        string folderResult = Encoding.UTF8.GetString(folderResponse);
                        JObject folderObj = JObject.Parse(folderResult);
                        
                        if (folderObj["id"] != null)
                        {
                            folderId = folderObj["id"].ToString();
                            config.DriveFolderId = folderId;
                            SaveConfig();
                        }
                    }

                    string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
                    client.Headers.Add("Content-Type", $"multipart/related; boundary={boundary}");

                    StringBuilder sb = new StringBuilder();
                    sb.Append("--" + boundary + "\r\n");
                    sb.Append("Content-Type: application/json; charset=UTF-8\r\n\r\n");
                    sb.Append(JsonConvert.SerializeObject(new
                    {
                        name = $"{backupName}.zip",
                        parents = new[] { folderId }
                    }));
                    sb.Append("\r\n--" + boundary + "\r\n");
                    sb.Append("Content-Type: application/zip\r\n\r\n");

                    byte[] metadataBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    byte[] endBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

                    byte[] postData = new byte[metadataBytes.Length + fileBytes.Length + endBytes.Length];
                    Buffer.BlockCopy(metadataBytes, 0, postData, 0, metadataBytes.Length);
                    Buffer.BlockCopy(fileBytes, 0, postData, metadataBytes.Length, fileBytes.Length);
                    Buffer.BlockCopy(endBytes, 0, postData, metadataBytes.Length + fileBytes.Length, endBytes.Length);

                    await client.UploadDataTaskAsync(
                        "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart",
                        "POST", postData);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Google Drive upload failed: {ex.Message}");
            }
        }

        private void OpenManager(object sender, EventArgs e)
        {
            if (managerForm == null || managerForm.IsDisposed)
            {
                managerForm = new BackupManagerForm(config, this);
            }
            managerForm.Show();
            managerForm.BringToFront();
        }

        public void ManualBackup(object sender, EventArgs e)
        {
            _ = Task.Run(() => PerformBackup());
        }

        // 2. Método para async (Task, com await)
        public async Task ManualBackupAsync()
        {
            await PerformBackup();
}
        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        public void RefreshConfig()
        {
            LoadConfig();
            if (!string.IsNullOrEmpty(config.DriveRefreshToken))
            {
                driveRefreshToken = config.DriveRefreshToken;
                Task.Run(() => RefreshDriveToken());
            }
        }

        public void SetDriveTokens(string accessToken, string refreshToken)
        {
            driveAccessToken = accessToken;
            driveRefreshToken = refreshToken;
            config.DriveRefreshToken = refreshToken;
            SaveConfig();
            
            if (managerForm != null && !managerForm.IsDisposed)
            {
                managerForm.UpdateDriveStatus(true);
            }
        }

        public void StartOAuthFlow()
        {
            Task.Run(() => StartOAuthServer());
        }

        private async Task StartOAuthServer()
        {
            try
            {
                oauthState = Guid.NewGuid().ToString();
                
                string redirectUri = "http://localhost:8080/oauth2callback";
                string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={Uri.EscapeDataString(googleConfig.ClientId)}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                    $"response_type=code&" +
                    $"scope=https://www.googleapis.com/auth/drive.file&" +
                    $"access_type=offline&" +
                    $"prompt=consent&" +
                    $"state={oauthState}";

                oauthListener = new HttpListener();
                oauthListener.Prefixes.Add("http://localhost:8080/oauth2callback/");
                oauthListener.Start();

                System.Diagnostics.Process.Start(authUrl);

                var context = await oauthListener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                string code = null;
                string state = null;

                if (request.QueryString["code"] != null)
                {
                    code = request.QueryString["code"];
                    state = request.QueryString["state"];
                }

                string responseString = state == oauthState && !string.IsNullOrEmpty(code) 
                    ? $@"<html><body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h1 style='color: #4CAF50;'>✅ Authorization Successful!</h1>
                        <p style='font-size: 18px;'>You can close this window and return to {APP_NAME}.</p>
                        <p style='color: #666;'>Your Firefox backups will now be synced to Google Drive.</p>
                        </body></html>"
                    : $@"<html><body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h1 style='color: #f44336;'>❌ Authorization Failed</h1>
                        <p style='font-size: 18px;'>Please try again from {APP_NAME}.</p>
                        </body></html>";
                
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                oauthListener.Stop();

                if (state == oauthState && !string.IsNullOrEmpty(code))
                {
                    await ExchangeCodeForTokens(code);
                }
                else
                {
                    MessageBox.Show("OAuth authorization failed or was cancelled.", 
                        APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OAuth flow failed: {ex.Message}", 
                    APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (oauthListener != null && oauthListener.IsListening)
                {
                    oauthListener.Stop();
                }
            }
        }

        private async Task ExchangeCodeForTokens(string code)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    var values = new NameValueCollection();
                    values["client_id"] = googleConfig.ClientId;
                    values["client_secret"] = googleConfig.ClientSecret;
                    values["code"] = code;
                    values["grant_type"] = "authorization_code";
                    values["redirect_uri"] = "http://localhost:8080/oauth2callback";

                    byte[] response = await client.UploadValuesTaskAsync(
                        "https://oauth2.googleapis.com/token", values);
                    
                    string json = Encoding.UTF8.GetString(response);
                    JObject result = JObject.Parse(json);
                    
                    string accessToken = result["access_token"]?.ToString();
                    string refreshToken = result["refresh_token"]?.ToString();

                    if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                    {
                        config.DriveClientId = googleConfig.ClientId;
                        config.DriveClientSecret = googleConfig.ClientSecret;
                        SetDriveTokens(accessToken, refreshToken);
                        
                        MessageBox.Show("✅ Successfully connected to Google Drive!", 
                            APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to get tokens from Google.", 
                            APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Token exchange failed: {ex.Message}", 
                    APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // No profiles found — fall back to folder browser
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
                // Only one profile — use it directly
                return profiles[0].Path;
            }

            // Multiple profiles — show selector dialog
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
            // Create and show debug console
            DebugConsole.Show();
            DebugConsole.Log("=== RESTORE STARTED ===");
            DebugConsole.Log($"zipPath: {zipPath}");

            // Block restore if Firefox is running
            if (IsFirefoxRunning())
            {
                DebugConsole.Log("ERROR: Firefox is running — aborting restore.");
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
                    DebugConsole.Log("User cancelled profile selection.");
                    return;
                }
                DebugConsole.Log($"User selected profilePath: {profilePath}");

                if (!Directory.Exists(profilePath))
                {
                    DebugConsole.Log("ERROR: Selected profile directory does not exist.");
                    MessageBox.Show($"Profile folder not found: {profilePath}",
                        APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    DebugConsole.Log("User cancelled restore.");
                    return;
                }

                // Create pre-restore backup
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string preRestoreBackup = Path.Combine(GetBackupDir(), $"pre_restore_backup_{timestamp}.zip");
                DebugConsole.Log($"Creating pre-restore backup: {preRestoreBackup}");
                await Task.Run(() => CreateBackupZip(profilePath, preRestoreBackup));
                DebugConsole.Log("Pre-restore backup created.");

                // Extract backup to temp folder
                string tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                DebugConsole.Log($"Extracting to temp dir: {tempExtractDir}");
                try
                {
                    Directory.CreateDirectory(tempExtractDir);
                    DebugConsole.Log("Temp dir created. Calling ZipFile.ExtractToDirectory...");
                    ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                    DebugConsole.Log("Zip extracted successfully.");

                    // The zip may have a root folder; find the actual content folder
                    string extractRoot = tempExtractDir;
                    var subDirs = Directory.GetDirectories(tempExtractDir);
                    var rootFiles = Directory.GetFiles(tempExtractDir);
                    DebugConsole.Log($"Temp root has {subDirs.Length} subdirs, {rootFiles.Length} files.");

                    if (subDirs.Length == 1 && rootFiles.Length == 0)
                    {
                        // Zip contained a single root folder — use its contents
                        extractRoot = subDirs[0];
                        DebugConsole.Log($"Using inner folder as extract root: {extractRoot}");
                    }

                    // Count extracted files
                    int extractedFiles = Directory.GetFiles(extractRoot, "*.*", SearchOption.AllDirectories).Length;
                    DebugConsole.Log($"Extracted {extractedFiles} files from {extractRoot}.");

                    // DELETE EVERYTHING inside the profile folder first
                    DebugConsole.Log("Clearing profile directory...");
                    ClearProfileDirectory(profilePath);
                    DebugConsole.Log("Profile directory cleared.");

                    // Copy all extracted files into the now-empty profile folder
                    DebugConsole.Log("Copying restored files to profile...");
                    var failedFiles = new List<string>();
                    int restoredCount = 0;

                    foreach (string file in Directory.GetFiles(extractRoot, "*.*", SearchOption.AllDirectories))
                    {
                        string relPath = GetRelativePath(extractRoot, file);
                        string destFile = Path.Combine(profilePath, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                        bool copied = TryCopyFileWithRetry(file, destFile, 3, 1000);
                        if (copied)
                            restoredCount++;
                        else
                        {
                            failedFiles.Add(relPath);
                            DebugConsole.Log($"FAILED to copy: {relPath}");
                        }
                    }

                    DebugConsole.Log($"Restore complete. {restoredCount} files copied, {failedFiles.Count} failures.");

                    string resultMessage = BuildRestoreResultMessage(
                        profilePath, preRestoreBackup, restoredCount, failedFiles);

                    MessageBox.Show(resultMessage, APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    DebugConsole.Log("Cleaning up temp directory...");
                    if (Directory.Exists(tempExtractDir))
                        Directory.Delete(tempExtractDir, true);
                    DebugConsole.Log("Cleanup done.");
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                DebugConsole.Log($"Stack trace:\n{ex.StackTrace}");
                MessageBox.Show($"Restore failed: {ex.Message}",
                    APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            DebugConsole.Log("=== RESTORE FINISHED ===");
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
    }
    public static class DebugConsole
    {
        private static Form _form;
        private static TextBox _textBox;
        private static readonly object _lock = new object();

        private static bool _enabled = false;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void Show()
        {
            if (!_enabled) return;

            lock (_lock)
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.BringToFront();
                    return;
                }

                _form = new Form
                {
                    Text = "FireKeeper Debug Console",
                    Size = new System.Drawing.Size(800, 500),
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
                _form.Show();
                Log("Debug console opened.");
            }
        }

        public static void Log(string message)
        {
            if (!_enabled) return;

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
                // Also write to a log file as fallback
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

    public class Config
    {
        public int BackupIntervalHours { get; set; }
        public int MaxBackups { get; set; }
        public string FirefoxProfilePath { get; set; }
        public string LastBackup { get; set; }
        public string DriveClientId { get; set; }
        public string DriveClientSecret { get; set; }
        public string DriveRefreshToken { get; set; }
        public string DriveFolderId { get; set; }
        public List<string> IncludeFolders { get; set; }
        public List<string> ExcludeFolders { get; set; }
        public List<string> ExcludeExtensions { get; set; }
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
        private System.Windows.Forms.NumericUpDown intervalBox;
        private System.Windows.Forms.NumericUpDown maxBackupsBox;
        private Label driveStatusLabel;
        private Button connectButton;
        private Button disconnectButton;
        private Label statusLabel;
        private const string APP_NAME = "FireKeeper";

        public BackupManagerForm(Config cfg, BackupTrayContext ctx)
        {
            config = cfg;
            context = ctx;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = APP_NAME + " - Backup Manager";
            this.Size = new System.Drawing.Size(520, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firekeeper.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    this.Icon = GetDefaultIcon();
                }
            }
            catch
            {
                this.Icon = GetDefaultIcon();
            }
            

            TableLayoutPanel mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Padding = new Padding(20);
            mainPanel.RowCount = 8;
            mainPanel.ColumnCount = 2;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Title
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
            subtitleLabel.Text = " - Firefox Backup & Sync";
            subtitleLabel.Font = new Font("Segoe UI", 12);
            subtitleLabel.ForeColor = Color.Gray;
            subtitleLabel.AutoSize = true;
            titlePanel.Controls.Add(subtitleLabel);
            
            mainPanel.Controls.Add(titlePanel, 0, 0);
            mainPanel.SetColumnSpan(titlePanel, 2);

            // Separator
            mainPanel.Controls.Add(new Label() { Text = "", Height = 2, BackColor = Color.LightGray }, 0, 1);
            mainPanel.SetColumnSpan(new Label() { Text = "" }, 2);

            // Google Drive section
            Label driveLabel = new Label();
            driveLabel.Text = "☁️ Google Drive:";
            driveLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            driveLabel.AutoSize = true;
            mainPanel.Controls.Add(driveLabel, 0, 2);

            FlowLayoutPanel drivePanel = new FlowLayoutPanel();
            drivePanel.FlowDirection = FlowDirection.LeftToRight;
            drivePanel.AutoSize = true;

            driveStatusLabel = new Label();
            driveStatusLabel.Text = string.IsNullOrEmpty(config.DriveRefreshToken) ? "🔴 Not connected" : "🟢 Connected";
            driveStatusLabel.AutoSize = true;
            driveStatusLabel.Font = new Font("Segoe UI", 9);
            drivePanel.Controls.Add(driveStatusLabel);

            connectButton = new Button();
            connectButton.Text = "Connect Google Account";
            connectButton.AutoSize = true;
            connectButton.Font = new Font("Segoe UI", 9);
            connectButton.Click += (s, e) => context.StartOAuthFlow();
            drivePanel.Controls.Add(connectButton);

            disconnectButton = new Button();
            disconnectButton.Text = "Disconnect";
            disconnectButton.AutoSize = true;
            disconnectButton.Font = new Font("Segoe UI", 9);
            disconnectButton.Enabled = !string.IsNullOrEmpty(config.DriveRefreshToken);
            disconnectButton.Click += (s, e) =>
            {
                config.DriveRefreshToken = null;
                config.DriveClientId = null;
                config.DriveClientSecret = null;
                config.DriveFolderId = null;
                context.RefreshConfig();
                UpdateDriveStatus(false);
            };
            drivePanel.Controls.Add(disconnectButton);

            mainPanel.Controls.Add(drivePanel, 1, 2);

            // Firefox profile
            Label profileLabel = new Label();
            profileLabel.Text = "📁 Firefox Profile:";
            profileLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            profileLabel.AutoSize = true;
            mainPanel.Controls.Add(profileLabel, 0, 3);

            FlowLayoutPanel profilePanel = new FlowLayoutPanel();
            profilePanel.FlowDirection = FlowDirection.LeftToRight;
            profilePanel.AutoSize = true;

            profilePathBox = new System.Windows.Forms.TextBox();
            profilePathBox.Text = config.FirefoxProfilePath;
            profilePathBox.Width = 300;
            profilePathBox.ReadOnly = true;
            profilePathBox.Font = new Font("Segoe UI", 9);
            profilePanel.Controls.Add(profilePathBox);

            Button browseBtn = new Button();
            browseBtn.Text = "📂 Select Profile...";
            browseBtn.AutoSize = true;
            browseBtn.Font = new Font("Segoe UI", 9);
            browseBtn.Click += (s, e) =>
            {
                string selected = context.ShowProfileSelector("Select the Firefox profile to back up:");
                if (!string.IsNullOrEmpty(selected))
                {
                    profilePathBox.Text = selected;
                }
            };
            profilePanel.Controls.Add(browseBtn);

            mainPanel.Controls.Add(profilePanel, 1, 3);

            // Backup settings
            Label settingsLabel = new Label();
            settingsLabel.Text = "⚙️ Backup Settings:";
            settingsLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            settingsLabel.AutoSize = true;
            mainPanel.Controls.Add(settingsLabel, 0, 4);
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
            mainPanel.Controls.Add(intervalPanel, 0, 5);
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
            mainPanel.Controls.Add(maxPanel, 0, 6);
            mainPanel.SetColumnSpan(maxPanel, 2);

            // Buttons
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.AutoSize = true;
            buttonPanel.Controls.Add(new Label() { Text = "", Width = 20 });

            Button saveBtn = new Button();
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
                context.RefreshConfig();
                MessageBox.Show("✅ Settings saved successfully!", APP_NAME, 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };
            buttonPanel.Controls.Add(saveBtn);

            Button backupNowBtn = new Button();
            backupNowBtn.Text = "🔄 Backup Now";
            backupNowBtn.Size = new System.Drawing.Size(130, 32);
            backupNowBtn.Font = new Font("Segoe UI", 9);
            backupNowBtn.BackColor = Color.FromArgb(255, 152, 0);
            backupNowBtn.ForeColor = Color.White;
            backupNowBtn.FlatStyle = FlatStyle.Flat;
            backupNowBtn.Click += async (s, e) =>
            {
                backupNowBtn.Enabled = false;
                backupNowBtn.Text = "⏳ Backing up...";
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

            mainPanel.Controls.Add(buttonPanel, 0, 7);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            // Status bar
            statusLabel = new Label();
            statusLabel.Text = "✅ Ready";
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            statusLabel.BackColor = Color.FromArgb(240, 240, 240);
            statusLabel.Padding = new Padding(5);
            statusLabel.Font = new Font("Segoe UI", 9);
            mainPanel.Controls.Add(statusLabel, 0, 8);
            mainPanel.SetColumnSpan(statusLabel, 2);
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Button restoreBtn = new Button();
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
                    dialog.InitialDirectory = GetBackupDirectory();
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        // Desabilitar botão durante a restauração
                        restoreBtn.Enabled = false;
                        restoreBtn.Text = "⏳ Restoring...";
                        statusLabel.Text = "⏳ Restoring backup...";
                        
                        try
                        {
                            await context.RestoreBackup(dialog.FileName);
                        }
                        finally
                        {
                            // Reabilitar botão
                            restoreBtn.Enabled = true;
                            restoreBtn.Text = "📥 Restore Backup";
                            statusLabel.Text = "✅ Ready";
                        }
                    }
                }
            };
            buttonPanel.Controls.Add(restoreBtn);

            this.Controls.Add(mainPanel);
        }

        public void UpdateDriveStatus(bool connected)
        {
            driveStatusLabel.Text = connected ? "🟢 Connected" : "🔴 Not connected";
            disconnectButton.Enabled = connected;
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

        private string GetBackupDirectory()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                APP_NAME, "backups");
            return Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}