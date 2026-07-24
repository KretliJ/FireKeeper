### `%APPDATA%\FireKeeper\config.json` (Application Settings)

This file is automatically created by FireKeeper and stored in your user profile:

Location: `C:\Users\[YourUsername]\AppData\Roaming\FireKeeper\config.json`

{
  "BackupIntervalHours": 24,
  "MaxBackups": 10,
  "FirefoxProfilePath": "C:\\Users\\user\\AppData\\Roaming\\Mozilla\\Firefox\\Profiles\\xxxx.default",
  "LastBackup": "20260121_153022",
  "SyncFolderPath": "C:\\Users\\user\\Desktop\\FireKeeper",
  "IncludeFolders": [
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
    "xulstore"
  ],
  "ExcludeFolders": [
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
  ],
  "ExcludeExtensions": [
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
  ]
}

### Configuration Fields Explained

#### Backup Settings

| Field               | Type    | Description                             | Default            |
| ------------------- | ------- | --------------------------------------- | ------------------ |
| BackupIntervalHours | Integer | Hours between automatic backups         | 24                 |
| MaxBackups          | Integer | Maximum number of backups to keep       | 10                 |
| FirefoxProfilePath  | String  | Path to your Firefox profile folder     | Auto-detected      |
| LastBackup          | String  | Timestamp of the last successful backup | Auto-generated     |
| SyncFolderPath      | String  | Folder where backups are stored         | Desktop\FireKeeper |

#### Backup Selection Rules

**Include Folders** - Files/folders that WILL be backed up:

- bookmarkbackups - Bookmark backups
- browser-extension-data - Extension data
- extensions - Installed extensions
- storage - Web storage data
- places.sqlite - History and bookmarks
- logins.json - Saved passwords
- prefs.js - Firefox preferences
- cookies.sqlite - Cookies
- sessionstore.jsonlz4 - Session restore data
- key3.db / key4.db - Password encryption keys
- xulstore.json - UI layout settings

**Exclude Folders** - Files/folders that WILL NOT be backed up:

- cache2 / cache - Browser cache (large, temporary)
- OfflineCache - Offline cache
- weave - Sync data (large)
- storage\default\https+++ - Large site storage
- thumbnails\failures - Failed thumbnail downloads
- datareporting\archived - Old telemetry data

**Exclude Extensions** - File types skipped during backup:

- .lock - Lock files
- .tmp / .temp - Temporary files
- .log - Log files
- .wal / .shm - SQLite temporary files
- .bak / .old - Backup files
- .corrupt - Corrupt files
