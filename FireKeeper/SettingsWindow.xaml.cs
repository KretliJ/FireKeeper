// SettingsWindow.xaml.cs - Settings/backup manager window (WPF, real Win11 Acrylic backdrop).
// Runs on its own dedicated thread/Dispatcher - see SettingsWindowHost.cs for why.
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace FireKeeper
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly Config config;
        private readonly BackupTrayContext context;
        private const string APP_NAME = "FireKeeper";

        public SettingsWindow(Config cfg, BackupTrayContext ctx)
        {
            config = cfg;
            context = ctx;

            InitializeComponent();

            // Loads WPF-UI's Fluent resource dictionaries onto this window even though the
            // app has no System.Windows.Application/App.xaml (the rest of FireKeeper is a
            // WinForms tray app) - this is the officially documented way to use WPF-UI without
            // an App.xaml. The Acrylic backdrop itself comes from WindowBackdropType in the XAML.
            ApplicationThemeManager.Apply(this);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firekeeper.ico");
                if (File.Exists(iconPath))
                    Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            catch { /* non-essential */ }

            ProfilePathBox.Text = config.FirefoxProfilePath;
            SyncFolderBox.Text = string.IsNullOrEmpty(config.SyncFolderPath)
                ? context.GetSyncFolder()
                : config.SyncFolderPath;
            IntervalBox.Text = config.BackupIntervalHours.ToString();
            MaxBackupsBox.Text = config.MaxBackups.ToString();
            StartupCheck.IsChecked = IsInStartup();
        }

        private bool IsInStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                {
                    return key?.GetValue("FireKeeper") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Called from BackupTrayContext.ProgressUpdate via SettingsWindowHost (already
        /// marshalled onto this window's Dispatcher, so it's safe to touch controls directly here).</summary>
        public void UpdateProgress(int percent, string status)
        {
            int clamped = Math.Min(100, Math.Max(0, percent));
            ProgressBarControl.IsIndeterminate = percent < 100;
            ProgressBarControl.Value = clamped;
            ProgressLabel.Text = status;
            StatusLabel.Text = percent >= 100 ? "✅ " + status : status;
        }

        public void ResetProgress()
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 0;
            ProgressLabel.Text = "Ready";
            StatusLabel.Text = "✅ Ready";
        }

        private void BrowseProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string selected = context.ShowProfileSelector("Select the Firefox profile to back up:");
            if (!string.IsNullOrEmpty(selected))
            {
                ProfilePathBox.Text = selected;
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder where backups will be stored " +
                    "(e.g. Google Drive, Dropbox, OneDrive, or any local folder):";
                dialog.SelectedPath = SyncFolderBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SyncFolderBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(IntervalBox.Text, out int interval) || interval <= 0)
            {
                MessageBox.Show("Interval (hours) must be a positive number.", APP_NAME,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(MaxBackupsBox.Text, out int maxBackups) || maxBackups <= 0)
            {
                MessageBox.Show("Max backups to keep must be a positive number.", APP_NAME,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            config.BackupIntervalHours = interval;
            config.MaxBackups = maxBackups;
            config.FirefoxProfilePath = ProfilePathBox.Text;
            config.SyncFolderPath = SyncFolderBox.Text;
            BackupTrayContext.SetStartup(StartupCheck.IsChecked == true);
            context.SaveConfigChanges();

            MessageBox.Show("✅ Settings saved successfully!", APP_NAME,
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private async void BackupNowButton_Click(object sender, RoutedEventArgs e)
        {
            BackupNowButton.IsEnabled = false;
            BackupNowButton.Content = "⏳ Running...";
            ProgressLabel.Text = "⏳ Creating backup...";
            StatusLabel.Text = "⏳ Creating backup...";

            try
            {
                await context.ManualBackupAsync();
                MessageBox.Show("✅ Backup completed successfully!", APP_NAME,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                StatusLabel.Text = "✅ Backup completed";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Backup failed: {ex.Message}", APP_NAME,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusLabel.Text = "❌ Backup failed";
            }
            finally
            {
                BackupNowButton.IsEnabled = true;
                BackupNowButton.Content = "🔄 Backup Now";
                if (StatusLabel.Text == "⏳ Creating backup...")
                    StatusLabel.Text = "✅ Ready";
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Backup to Restore",
                Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
                InitialDirectory = context.GetSyncFolder()
            };

            if (dialog.ShowDialog() != true) return;

            RestoreButton.IsEnabled = false;
            RestoreButton.Content = "⏳ Restoring...";
            StatusLabel.Text = "⏳ Restoring backup...";
            ProgressLabel.Text = "⏳ Restoring backup...";

            try
            {
                await context.RestoreBackup(dialog.FileName);
            }
            finally
            {
                RestoreButton.IsEnabled = true;
                RestoreButton.Content = "📥 Restore Backup";
                StatusLabel.Text = "✅ Ready";
                ProgressLabel.Text = "Ready";
            }
        }
    }
}
