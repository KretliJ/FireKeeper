## ❓ General Questions

Q: Why does my backup take so long?
A: The first backup may take several minutes depending on profile size. Subsequent backups are faster as only changed files are processed and cached files are skipped.

Q: Can I use this with multiple Firefox profiles?
A: Yes! In the manager, browse to the profile folder you want to backup. You can switch profiles by changing the path in settings.

Q: Where are my backups stored?
A: Locally at %APPDATA%\FireKeeper\backups\ and synced to Google Drive (if connected).

Q: Is it safe to restore a backup?
A: Yes! FireKeeper creates a pre-restore backup automatically before overwriting your profile. You can always revert to the pre-restore backup if needed.

Q: Can I use this without Google Drive?
A: Yes! You can use FireKeeper for local backups only - just skip the Google Drive connection step.

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

Q: Is my data encrypted before upload?
A: Backups are compressed in ZIP format. For additional security, you can encrypt the ZIP file manually or use a separate encryption tool.

Q: Why do I get "Google Drive credentials not found" error?
A: You need to create appsettings.json in the same folder as FireKeeper.exe with your Google OAuth credentials.

Q: How do I reset my Google Drive connection?
A: Click "Disconnect" in the manager, or delete the DriveRefreshToken and DriveFolderId fields from config.json.

### 🔎 Troubleshooting

Q: The app won't start - what do I do?
A: Check that:

- .NET Framework 4.8 is installed
- appsettings.json exists and is valid
- You have write permissions to %APPDATA%\FireKeeper\

Q: Backup fails with "Access denied" error
A: FireKeeper may not have read permissions for your Firefox profile. Run FireKeeper as Administrator or check folder permissions.

Q: Google Drive upload fails
A: Try:

1. Disconnect and reconnect your Google Drive account
2. Check your internet connection
3. Verify your Google Drive has enough free space

Q: Restore fails with "Firefox is running"
A: Close Firefox completely (check Task Manager for any leftover processes) and try again.

Q: The app uses a lot of memory during backup
A: Memory usage spikes during backup (compression is memory-intensive). Normal idle memory is ~5MB.
