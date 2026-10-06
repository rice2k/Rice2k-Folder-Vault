# UI / UX Specification

## Brand

**Product:** Rice2k Folder Vault  
**Visual direction:** professional Windows 11 utility, dark navy surfaces, restrained blue highlights, clear lock-state colors, minimal decoration.

## Core colors

| Token | Value | Usage |
|---|---|---|
| App background | `#0B1220` | main window |
| Panel | `#111B2E` | cards and grouped controls |
| Panel border | `#243453` | subtle separation |
| Primary | `#1677FF` | primary action |
| Primary hover | `#2E89FF` | hover |
| Text primary | `#F4F7FB` | headings/body |
| Text secondary | `#AAB8CC` | descriptive text |
| Locked | `#C9434D` | locked state |
| Unlocked | `#33C46A` | unlocked state |
| Warning | `#E5A52A` | auto-lock warning |

## Icon system

The application icon combines a **folder**, **shield**, and **padlock**. Tray icons should be simpler so they remain readable at 16–24 px.

States:

- Locked — closed silver/blue padlock
- Unlocked — open blue/green padlock
- Auto-lock soon — amber padlock
- Error — red padlock/shield

Source SVG references are stored under `assets/icons/`. Final Windows `.ico` files will be generated from reviewed source artwork.

## Main window

The main window contains:

- product identity
- active vault name
- current state badge
- mount location
- auto-lock countdown/status
- Open Vault / Unlock Vault
- Lock Now
- Settings
- Vault Manager
- storage usage when the encrypted storage engine is connected

The main window should never imply that encryption is active when the storage engine is not actually connected.

## Unlock prompt

Title: **Unlock — Rice2k Folder Vault**

Primary text:

> Personal Vault is locked

Secondary text:

> Enter your password to unlock.

Controls:

- password input
- Show password
- Unlock Vault
- Cancel
- Recovery options only when the vault has a configured recovery slot

Password-manager paste must remain enabled.

## Lock confirmation

When manually locking with active files:

> **Lock the vault now?**  
> One or more files are still open. Locking now may make those files unavailable.

Actions:

- Wait for files to close
- Lock Anyway
- Cancel

For normal manual locking with no active files, no confirmation is necessary unless the user enables confirmation in settings.

## Auto-lock warning

> **Vault will lock in 30 seconds**  
> Rice2k Folder Vault has been inactive and will lock automatically.

Actions:

- Keep Unlocked
- Lock Now

## Settings navigation

Planned categories:

1. General
2. Security
3. Auto-Lock
4. Vault
5. Appearance
6. Notifications
7. Hotkeys
8. Startup
9. Advanced
10. About

## Locked storage view

While locked, protected names and contents must not be exposed. The user may see the outer encrypted container/storage object, but not plaintext files from inside the vault.

## Accessibility

- Do not rely on color alone to communicate Locked/Unlocked.
- Maintain readable text contrast.
- Support keyboard navigation.
- Provide visible focus states.
- Avoid tiny click targets in tray-adjacent UI.
- Keep important prompts concise and explicit.
