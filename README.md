# Rice2k Folder Vault

**Rice2k Folder Vault** is a Windows encrypted-vault application designed to make protected files feel as easy to use as a normal folder while keeping the stored data encrypted whenever the vault is locked.

> Current status: **v0.1.0-alpha — foundation / prototype**

## Project goals

- Password-protected encrypted vaults
- Drag-and-drop use through Windows File Explorer
- System-tray lock/unlock controls
- Automatic relocking after inactivity
- Lock on Windows lock, sign-out, sleep, hibernate, shutdown, or restart
- Safe handling of open files before unmounting
- Optional recovery key
- Multiple-vault support in later releases
- Windows Hello integration in a later release
- No custom/home-grown cryptographic algorithms

## Intended user experience

1. Start Rice2k Folder Vault.
2. A vault is locked by default.
3. Unlock it with the vault password.
4. The vault mounts as a normal Windows drive/folder.
5. Drag, copy, edit, rename, and delete files normally.
6. Lock manually or allow an auto-lock rule to trigger.
7. The mounted view disappears and only encrypted vault data remains.

## Security direction

The design calls for authenticated encryption and password-based key derivation. A random vault master key will protect vault contents, while a key derived from the user's password will protect the master key. Passwords themselves must never be stored.

See [Security Design](docs/SECURITY-DESIGN.md) for the architecture and threat model.

## Repository layout

```text
Rice2k-Folder-Vault/
├─ src/Rice2k.FolderVault/
│  ├─ Models/
│  ├─ Services/
│  └─ Views/
├─ docs/
│  ├─ PRODUCT-SPEC.md
│  └─ SECURITY-DESIGN.md
├─ .github/workflows/
├─ CHANGELOG.md
├─ ROADMAP.md
├─ VERSION
└─ README.md
```

## Development stack

The initial desktop shell uses **C# / .NET 8 / WPF** for Windows integration, tray controls, dialogs, timers, and future lifecycle-event handling.

A proven Windows filesystem layer will be integrated for mounting the encrypted vault. The project will not invent its own filesystem driver or cryptographic algorithm.

## Current milestone — v0.1.0-alpha

This milestone establishes:

- professional Windows application shell
- locked/unlocked state model
- unlock prompt prototype
- main control panel
- settings model
- tray-icon foundation
- auto-lock timer foundation
- project/version documentation
- automated Windows build validation

**Important:** v0.1.0-alpha does not yet contain the production encrypted-storage engine or filesystem mount. Do not use it to protect sensitive data.

## Build

Prerequisites:

- Windows 10/11
- .NET 8 SDK

```powershell
dotnet restore src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
dotnet build src/Rice2k.FolderVault/Rice2k.FolderVault.csproj -c Release
dotnet run --project src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
```

## Versioning

Rice2k Folder Vault follows semantic versioning.

- **MAJOR** — incompatible vault/application changes
- **MINOR** — new backward-compatible features
- **PATCH** — fixes and small improvements
- `-alpha` / `-beta` — pre-release builds

See [CHANGELOG.md](CHANGELOG.md) and [ROADMAP.md](ROADMAP.md).

## Safety during development

Until the encrypted storage engine is marked stable, use only disposable test files and keep independent backups.

## Author

A Rice2k project.
