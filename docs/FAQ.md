## ❓ General Questions

Q: Why does my backup take so long?
A: The first backup may take several minutes depending on profile size. Subsequent backups are faster as only changed files are processed and cached files are skipped.

Q: Can I use this with multiple Firefox profiles?
A: Yes! In the manager, click "Select..." to choose which profile folder to back up. You can switch profiles by changing the path in settings.

Q: Where are my backups stored?
A: In the Sync Folder you configured (default: Desktop\FireKeeper). You can change this in the manager.

Q: How do I sync to the cloud?
A: Set your Sync Folder to a folder that your cloud client watches (Google Drive, Dropbox, OneDrive, etc.). The cloud client handles the upload automatically.

Q: Is it safe to restore a backup?
A: Yes! FireKeeper creates a pre-restore backup automatically before overwriting your profile. You can always revert to the pre-restore backup if needed.
⚠️ Warning: Restoring will retain your credentials but many services will log you out for obvious security reasons 

Q: Can I use this without cloud sync?
A: Yes! Just set the Sync Folder to a local path and use it as a local-only backup tool.

Q: Will this work if Firefox is open?
A: FireKeeper will warn you if Firefox is running before restoring. Backups can run with Firefox open, but restoration requires Firefox to be closed.

Q: How much disk space do backups use?
A: Backups typically range from 50-500MB depending on your profile size. FireKeeper automatically manages backups and keeps only the maximum number you specify.

Q: Does it backup my passwords?
A: Yes! logins.json and encryption keys (key3.db, key4.db) are included in the backup.

### 🔎 Technical Questions

Q: What files are excluded from backup?
A: Cache files, temporary files, lock files, log files, and SQLite temporary files are excluded. See the Configuration section for the complete list.

Q: Can I customize what gets backed up?
A: Yes! Edit the IncludeFolders, ExcludeFolders, and ExcludeExtensions lists in config.json.

Q: Does FireKeeper run as a service?
A: No, it runs as a user application with a system tray icon. It stays resident in the background until you exit.

Q: Is my data encrypted?
A: Backups are compressed in ZIP format. For additional security, you can encrypt the ZIP file manually or use a separate encryption tool.

Q: Why don't I need Google OAuth credentials anymore?
A: FireKeeper 2.0 dropped built-in OAuth in favor of a universal sync folder approach. You bring your own sync client (Google Drive, Dropbox, OneDrive, etc.) and FireKeeper just writes files to a folder. See LEGACY.md for more details.

Q: How do I reset my configuration?
A: Delete %APPDATA%\Roaming\FireKeeper\config.json and restart FireKeeper. A new config will be created with defaults.

### 🔎 Troubleshooting

Q: The app won't start - what do I do?
A: Check that:
- .NET Framework 4.8 is installed
- You have write permissions to %APPDATA%\Roaming\FireKeeper\
- The config.json file is not corrupted

Q: Backup fails with "Access denied" error
A: FireKeeper may not have read permissions for your Firefox profile. Run FireKeeper as Administrator or check folder permissions.

Q: My sync folder isn't syncing to the cloud
A: Make sure your cloud client (Google Drive, Dropbox, etc.) is running and properly configured. FireKeeper just writes files to the folder, the cloud client handles the upload.

Q: Restore fails with "Firefox is running"
A: Close Firefox completely (check Task Manager for any leftover processes) and try again.

Q: The app uses a lot of memory during backup
A: Memory usage spikes during backup (compression is memory-intensive). Normal idle memory is ~31MB.