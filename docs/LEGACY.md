# 🔥 FireKeeper Legacy Guide (v1.x)

This document is for **FireKeeper 1.x** with the built-in **Google Drive OAuth** integration, to understand what changed and why.

---

## What Changed in v2.0?

FireKeeper 2.0 removed the built-in Google Drive OAuth integration in favor of a **universal sync folder** approach. Instead of the app managing its own cloud uploads via Google APIs, it now writes backups to any folder you choose and you bring your own sync client.

### Why the change?

| Problem (v1.x)                                                        | Solution (v2.0+)                                |
| --------------------------------------------------------------------- | ----------------------------------------------- |
| Complex OAuth setup (Cloud Console, Client ID/Secret, consent screen) | Zero setup, just pick a folder                  |
| Google API quotas and rate limits                                     | No API limits, it's just file copies           |
| OAuth tokens expiring, refresh failures                               | No tokens to manage                             |
| Locked to Google Drive only                                           | Works with**any** sync service or storage |
| Unverified app restrictions (100 user cap)                            | No app verification needed                      |
| 200 lines of fragile HTTP/auth code                                   | Simple, robust file I/O                         |

The old OAuth code was essentially a "flex" — it worked, but it solved a problem that desktop sync clients already solved better. v2.0 embraces "your storage, your rules."

---

## Legacy path

### ⚙️ Setup Guide

### 1. Google Drive Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or select existing)
3. Enable the **Google Drive API**
4. Configure the **OAuth consent screen**:
   - App name: `FireKeeper`
   - User support email: your email
   - Scopes: `.../auth/drive.file`
   - Add your email as a **test user**
5. Create **OAuth Client ID** (Desktop app type)
6. Copy your **Client ID** and **Client Secret**

### `appsettings.json` (Google Drive Credentials)

Create this file in the same folder as `FireKeeper.exe`:

{
  "GoogleDrive": {
    "ClientId": "your_client_id.apps.googleusercontent.com",
    "ClientSecret": "your_client_secret"
  }
}

| Field        | Description                                            |
| ------------ | ------------------------------------------------------ |
| ClientId     | Your OAuth 2.0 Client ID from Google Cloud Console     |
| ClientSecret | Your OAuth 2.0 Client Secret from Google Cloud Console |

⚠️ Never share your Client Secret or commit this file to version control!

### 2. Configure FireKeeper

Rename `appsettings.example.json` (in the same folder as FireKeeper.exe) to `appsettings.json`.

Paste here your ClientId and ClientSecret generated from your OAuth:

```json
{
  "GoogleDrive": {
    "ClientId": "your_client_id.apps.googleusercontent.com",
    "ClientSecret": "your_client_secret"
  }
}
```

### 3. First Run

1. Launch FireKeeper.exe
2. Right click tray icon > Open Manager
3. Click "Connect Google Account"
4. Authorize the app in your browser
5. Done! Your Firefox profile will now be backed up automatically

---

## Current recommended path

### 1. Install Google Drive Desktop (or your preferred sync client)

- [Google Drive Desktop](https://www.google.com/drive/download/)
- [Dropbox](https://www.dropbox.com/download)
- [OneDrive](https://www.microsoft.com/en-us/microsoft-365/onedrive/download)
- Or any other sync tool you prefer

### 2. Set your sync folder in FireKeeper 2.0+

1. Open FireKeeper Manager (double-click the tray icon)
2. Next to **Sync Folder**, click **Browse...**
3. Navigate to your sync client's folder, e.g.:
   - `C:\Users\<You>\Google Drive\FireKeeper`
   - `C:\Users\<You>\Dropbox\FireKeeper`
   - `C:\Users\<You>\OneDrive\FireKeeper`
4. Click **Save Settings**

FireKeeper will now copy every new backup to that folder. Your sync client handles the upload.

---

## Can I Still Use v1.x?

Yes. The v1.x code and release still work and remain in the Git history, if you prefer the built-in OAuth approach.

**Note:** v1.x is unmaintained. No bug fixes, security patches, or feature updates will be backported.

---

## Technical: What Was Removed?

The following code and concepts were deleted in v2.0:

- `GoogleDriveConfig` class (`ClientId`, `ClientSecret`, `RefreshToken`, `DriveFolderId`)
- `RefreshDriveToken()` — token refresh loop
- `UploadToDrive()` — multipart HTTP upload to `googleapis.com`
- `StartOAuthServer()` — local `HttpListener` on `localhost:8080`
- `ExchangeCodeForTokens()` — OAuth code exchange
- `appsettings.json` in favor of dynamic and internal configuration
- Tray menu "Connect Google Account" / "Disconnect" buttons

All replaced by:

- `SyncFolder` string in `appsettings.json`
- `LoadSyncFolder()` / `SaveSyncFolder()` / `GetSyncFolder()` / `SetSyncFolder()`
- One `File.Copy()` call in `PerformBackup()`

---

## FAQ

**Q: Is the sync folder approach less secure?**
A: No. The security model is identical — your backups are unencrypted ZIP files either way. The difference is who handles the transport. With v1.x, FireKeeper did HTTPS uploads itself. With v2.0, your sync client does it. Both use TLS. Both store files in the cloud provider you chose.

**Q: What if I don't want cloud sync at all?**
A: Leave the sync folder at its default (`Documents\FireKeeper`) or set it to a local-only path.

**Q: Can I sync to multiple clouds?**
A: Yes. Point FireKeeper to a folder that multiple sync clients watch, or use a tool like [rclone](https://rclone.org/) to mirror your backup folder to several providers.

**Q: Will v2.0 ever add built-in cloud upload back?**
A: Unlikely. The sync folder approach is simpler, more robust, and universally compatible. It is the intended architecture going forward.

---
