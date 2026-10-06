# Product Specification

## Product name

**Rice2k Folder Vault**

## Purpose

Rice2k Folder Vault is intended to provide a simple Windows experience for storing files inside an encrypted vault. The user unlocks the vault, works with it through File Explorer like normal storage, and locks it again when finished.

## Core user experience

### Locked

- Vault contents are not mounted in Explorer.
- The user sees only the encrypted vault storage/container.
- Double-clicking the tray icon opens the unlock experience.
- Opening the main application shows the vault as locked.

### Unlock

The unlock dialog shows:

- vault name
- password field
- show/hide password
- Unlock button
- Cancel button
- recovery option when a recovery key exists

The application must support password-manager paste.

### Unlocked

- Vault appears as a normal Windows drive or mount point.
- Files can be dragged in and out using Explorer.
- Applications can open and save files through normal Windows file APIs.
- The control panel shows unlocked status, mount location, storage usage, and time remaining until auto-lock.

### Lock

The user may lock from:

- main control panel
- tray menu
- configured hotkey
- automatic lock rules

Before unmounting, the application must flush pending data and handle open files safely.

## Tray behavior

### Locked menu

- Unlock Vault
- Settings
- Vault Manager
- Create New Vault
- Help
- Exit

### Unlocked menu

- Open Vault
- Lock Now
- Settings
- Vault Manager
- Help
- Exit

Default double-click behavior:

- locked: open unlock dialog
- unlocked: open the mounted vault in Explorer

## Auto-lock options

Planned rules:

- lock after configurable inactivity
- lock when Windows locks
- lock when the user signs out
- lock on sleep/hibernate
- lock on shutdown/restart
- optionally lock when screensaver starts
- warning countdown before automatic lock

For open files, supported policies should include:

1. warn and wait
2. cancel automatic lock until handles close
3. force lock only when explicitly enabled

The safe default is **warn and wait**.

## Settings areas

- General
- Security
- Auto-Lock
- Vault
- Appearance
- Notifications
- Hotkeys
- Startup
- Advanced
- About

## Recovery model

Recovery must never be a secret vendor backdoor.

Two supported models are planned:

- **Maximum Security:** no recovery key; losing all unlock credentials means losing access.
- **Recovery Key:** generate a high-entropy recovery key during vault creation and use it as an independent key-wrapping credential.

## Logging

Application logs must not contain:

- passwords
- password-derived keys
- master keys
- recovery keys
- file contents
- filenames inside a vault
- full sensitive paths

## Non-goals

- pretending a normal plaintext folder is secure by merely hiding it
- custom cryptographic algorithms
- secretly recoverable passwords
- cloud storage as a requirement
