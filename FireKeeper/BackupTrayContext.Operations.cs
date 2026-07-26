// BackupTrayContext.Operations.cs - Backup/restore engine, scheduler, and tray menu command handlers
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FireKeeper
{
    public partial class BackupTrayContext
    {
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

        public string GetSyncFolder()
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

        public void CreateBackupZip(string sourceDir, string destZip)
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

        public bool ShouldIncludeFile(string relPath, HashSet<string> includeFolders)
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

        public bool ShouldSkipFile(string relPath, HashSet<string> excludeFolders, HashSet<string> excludeExtensions)
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

        public string GetRelativePath(string basePath, string fullPath)
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
            if (settingsHost == null)
            {
                settingsHost = new SettingsWindowHost(config, this);
                ProgressUpdate += settingsHost.UpdateProgress;

                settingsHost.Closed += () =>
                {
                    ProgressUpdate -= settingsHost.UpdateProgress;
                    settingsHost = null;

                    // Don't call Application.Exit() here - the tray icon keeps running.
                    DebugConsole.Log("Settings window closed.");
                };
            }

            settingsHost.ShowOrActivate();
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
            DebugConsole.Log("Exit requested. Cleaning up...");
            
            try
            {
                // 1. Close manager if open
                if (settingsHost != null && settingsHost.IsOpen)
                {
                    DebugConsole.Log("Closing settings window...");
                    settingsHost.Close();
                    settingsHost = null;
                }

                // 2. Stop backup timer
                if (backupTimer != null)
                {
                    DebugConsole.Log("Stopping backup timer...");
                    backupTimer.Stop();
                    backupTimer.Dispose();
                    backupTimer = null;
                }

                // 3. Remove and free tray icon
                if (trayIcon != null)
                {
                    DebugConsole.Log("Removing tray icon...");
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }

                // 4. Wait to ensure all is freed
                System.Threading.Thread.Sleep(100);

                DebugConsole.Log("Calling Application.Exit()...");
                Application.Exit();
                
                // 5. Fallback: force exit if Application.Exit() doesn't work
                System.Threading.Thread.Sleep(200);
                
                // If still running, force
                if (System.Diagnostics.Process.GetCurrentProcess().HasExited == false)
                {
                    DebugConsole.Log("Application.Exit() did not terminate. Forcing exit...");
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Error during exit: {ex.Message}");
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

        public void ClearProfileDirectory(string profilePath)
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

        public bool IsFirefoxRunning()
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

        // Auxiliary test methods
        public void SetMaxBackups(int maxBackups)
        {
            if (config != null)
                config.MaxBackups = maxBackups;
        }

        public void CleanDirectory(string directory, int maxBackups)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return;

                var backupFiles = Directory.GetFiles(directory, "firekeeper_backup_*.zip")
                    .OrderBy(f => f)
                    .ToList();

                while (backupFiles.Count > maxBackups)
                {
                    File.Delete(backupFiles[0]);
                    backupFiles.RemoveAt(0);
                }
            }
            catch { }
        }

        public void SetSyncFolder(string path)
        {
            if (config != null)
                config.SyncFolderPath = path;
        }
    }
}
