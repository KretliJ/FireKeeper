
## 🎯 Usage

### System Tray Menu

Right-click the FireKeeper icon in your system tray:

| Menu Item    | Description                        |
| ------------ | ---------------------------------- |
| Open Manager | Open the main configuration window |
| Backup Now   | Perform an immediate manual backup |
| Exit         | Close FireKeeper                   |

### Main Interface

#### Google Drive Section

| Button                 | Description                                              |
| ---------------------- | -------------------------------------------------------- |
| Connect Google Account | Link your Google Drive account (opens browser for OAuth) |
| Disconnect             | Remove Google Drive connection                           |

#### Firefox Profile Section

| Button    | Description                                 |
| --------- | ------------------------------------------- |
| Browse... | Select your Firefox profile folder manually |

#### Backup Settings

| Setting             | Description                                      |
| ------------------- | ------------------------------------------------ |
| Interval (hours)    | How often to run automatic backups (1-168 hours) |
| Max backups to keep | Maximum number of backups stored locally (1-50)  |

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
- Only creates backups if Firefox profile has changed
- Uploads to Google Drive if connected

### Manual Backup

To create a backup immediately:

- Right-click the tray icon and select "Backup Now"
- OR open the manager and click "Backup Now"

### Backup Location

Local backups are stored at:
%APPDATA%\FireKeeper\backups\

Example: C:\Users\YourUsername\AppData\Roaming\FireKeeper\backups\firekeeper_backup_20260121_153022.zip
