## 🎯 Usage

### System Tray Menu

Right-click the FireKeeper icon in your system tray:

| Menu Item          | Description                        |
| ------------------ | ---------------------------------- |
| Open Manager       | Open the main configuration window |
| Backup Now         | Perform an immediate manual backup |
| Toggle Debug Console | Open or close the debug console |
| Exit               | Close FireKeeper                   |

### Main Interface

#### Firefox Profile Section

| Button     | Description                                 |
| ---------- | ------------------------------------------- |
| Select...  | Select your Firefox profile folder manually |

#### Sync Folder Section

| Button     | Description                                 |
| ---------- | ------------------------------------------- |
| Browse...  | Select folder where backups will be stored  |

The sync folder can be any location:
- Google Drive folder (auto-sync to cloud)
- Dropbox folder (auto-sync to cloud)
- OneDrive folder (auto-sync to cloud)
- Any local folder (local-only backups)

#### Backup Settings

| Setting             | Description                                      |
| ------------------- | ------------------------------------------------ |
| Interval (hours)    | How often to run automatic backups (1-168 hours) |
| Max backups to keep | Maximum number of backups stored (1-50)          |

#### Actions

| Button            | Description                         |
| ----------------- | ----------------------------------- |
| 💾 Save Settings  | Save all configuration changes      |
| 🔄 Backup Now     | Create an immediate backup          |
| 📥 Restore Backup | Restore from a selected backup file |

### Restoring a Backup

1. Click "Restore Backup" in the main window
2. Select a .zip backup file from the dialog
3. Confirm the restore operation
4. FireKeeper will:
   - Check if Firefox is running (warning if open)
   - Create a pre-restore backup automatically
   - Extract and overwrite your profile files
   - Show success/failure notification

⚠️ Warning: Restoring will overwrite your current Firefox profile. A backup is automatically created before restoration.

### Automatic Backups

FireKeeper runs automatically in the background:
- Checks every minute if a backup is due
- Backups run according to your configured interval
- Cleans old backups automatically (keeps max you specified)

### Manual Backup

To create a backup immediately:
- Right-click the tray icon and select "Backup Now"
- OR open the manager and click "Backup Now"

### Backup Location

Backups are stored in your configured Sync Folder:
- Default: Desktop\FireKeeper
- Configurable via the manager

Example: `C:\Users\YourUsername\Desktop\FireKeeper\firekeeper_backup_20260121_153022.zip`

### Debug Console

Toggle the debug console from the tray menu to see detailed logs:
- File-by-file backup progress
- Skip/exclude reasons
- Compression status
- Error details