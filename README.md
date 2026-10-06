# Rice2k Folder Vault

**Rice2k Folder Vault** is a Windows encrypted-vault application designed to make protected files feel as easy to use as a normal folder while keeping stored data encrypted whenever the vault is locked.

> Current status: **v0.2.0-alpha — vault-format and cryptography development**

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
2. Create or select a `.rvault` container.
3. Unlock it with the vault password.
4. The vault eventually mounts as a normal Windows drive/folder.
5. Drag, copy, edit, rename, and delete files normally.
6. Lock manually or allow an auto-lock rule to trigger.
7. The mounted view disappears and only encrypted vault data remains.

## What v0.2.0-alpha currently does

The application now has a real per-vault cryptographic foundation:

- creates portable `.rvault` containers
- generates a random 256-bit Vault Master Key
- derives the password key with Argon2id
- wraps the master key with AES-256-GCM
- stores KDF parameters and wrapped-key material in a versioned vault header
- creates an encrypted/authenticated metadata segment
- validates metadata before unlock completes
- keeps the active master key only in the unlocked session and clears it on lock
- changes passwords by re-wrapping the master key
- implements chunked streaming AES-GCM file-content encryption as the next storage layer
- includes tamper/round-trip crypto self-tests

The **Explorer mount and persistent FILE-record integration are not finished yet**. Do not use an alpha build as the only copy of sensitive data.

## Security design

Passwords are never stored in plaintext. A password-derived Key Encryption Key protects a random Vault Master Key; HKDF-derived subkeys protect metadata and file records.

See:

- [Security Design](docs/SECURITY-DESIGN.md)
- [Vault Format](docs/VAULT-FORMAT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [UI Specification](docs/UI-SPEC.md)

## Repository layout

```text
Rice2k-Folder-Vault/
├─ src/Rice2k.FolderVault/
│  ├─ Models/
│  ├─ Services/
│  └─ Views/
├─ tests/Rice2k.FolderVault.CryptoSelfTest/
├─ assets/icons/
├─ docs/
│  ├─ ARCHITECTURE.md
│  ├─ PRODUCT-SPEC.md
│  ├─ SECURITY-DESIGN.md
│  ├─ UI-SPEC.md
│  └─ VAULT-FORMAT.md
├─ .github/workflows/
├─ CHANGELOG.md
├─ ROADMAP.md
├─ VERSION
└─ README.md
```

## Development stack

- C#
- .NET 8
- WPF
- Windows notification-area integration
- platform AES-GCM and HKDF primitives
- Konscious.Security.Cryptography.Argon2 for Argon2id password derivation

The filesystem layer will use a proven Windows user-mode filesystem solution rather than a custom kernel driver.

## Build

Prerequisites:

- Windows 10/11
- .NET 8 SDK

```powershell
dotnet restore src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
dotnet build src/Rice2k.FolderVault/Rice2k.FolderVault.csproj -c Release
dotnet run --project src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
```

Run the dependency-light crypto self-test:

```powershell
dotnet run --project tests/Rice2k.FolderVault.CryptoSelfTest/Rice2k.FolderVault.CryptoSelfTest.csproj -c Release
```

## Versioning

Rice2k Folder Vault follows semantic versioning for the application. The vault-format version is tracked separately.

- **MAJOR** — incompatible application changes
- **MINOR** — new backward-compatible features
- **PATCH** — fixes and small improvements
- `-alpha` / `-beta` — pre-release builds

See [CHANGELOG.md](CHANGELOG.md) and [ROADMAP.md](ROADMAP.md).

## Safety during development

Until the encrypted storage engine, filesystem layer, crash recovery, and security review are complete, use disposable test data and keep independent backups.

## Author

A Rice2k project.
