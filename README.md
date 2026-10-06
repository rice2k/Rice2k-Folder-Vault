# Rice2k Folder Vault

**Rice2k Folder Vault** is a Windows encrypted-vault application designed to make protected files feel as easy to use as a normal folder while keeping stored data encrypted whenever the vault is locked.

> Current status: **v0.3.0-alpha — read-only Explorer drive integration implemented; Windows runtime verification pending**

## Project goals

- Password-protected encrypted vaults
- Drag-and-drop use through Windows File Explorer
- System-tray lock/unlock controls
- Automatic relocking after inactivity
- Lock on Windows lock, sign-out, sleep, hibernate, shutdown, or restart
- Safe handling of open files before unmounting
- Optional recovery key
- Multiple-vault support
- Windows Hello integration in a later release
- No custom/home-grown cryptographic algorithms

## Current user flow

1. Start Rice2k Folder Vault.
2. Create a portable `.rvault` container.
3. Unlock it with the vault password.
4. Open **Vault Contents** to manage encrypted files/folders directly, or choose **Mount Explorer Drive**.
5. The v0.3 alpha mounts the unlocked vault as a **read-only** Windows drive such as `V:\` through DokanNet.
6. Explorer enumeration resolves names and hierarchy from encrypted `META` metadata.
7. File reads decrypt only the requested authenticated FILE ranges; no plaintext staging file is created for mounted reads.
8. Create/write/delete/rename through the mounted drive are denied until writable transactions are designed.
9. Locking requests an unmount before the active in-memory VMK is destroyed.

## What v0.3.0-alpha implements

- portable `.rvault` containers
- random 256-bit Vault Master Key (VMK)
- Argon2id password-based Key Encryption Key derivation
- AES-256-GCM VMK wrapping
- versioned and authenticated vault header
- HKDF-SHA256 domain-separated metadata/content keys
- authenticated encrypted metadata
- chunked streaming AES-256-GCM file encryption
- persistent encrypted `FILE` records inside the container
- encrypted metadata updates when files are imported
- file export/decryption by encrypted content-record ID
- password changes by re-wrapping the VMK rather than re-encrypting stored files
- Change Password UI in the main window and system tray
- in-memory VMK disposal/zeroing on lock
- alpha Vault Contents import/export interface
- encrypted metadata rename without decrypting file payloads
- file deletion with encrypted record compaction
- manual vault compaction that drops unreferenced encrypted FILE records
- encrypted nested directory entries and hierarchy
- create/browse/rename/delete encrypted folders
- import files directly into the currently open encrypted folder
- entry-ID based export/delete/rename so identical filenames can exist in different folders
- read-only DokanNet Explorer filesystem adapter
- mount/unmount lifecycle service with `WriteProtection`, Mount Manager, and current-session mounting
- read-only Explorer enumeration for encrypted nested folders
- persistent authenticated range reads by entry ID
- tray and main-window Mount/Unmount Explorer Drive controls
- pre-lock unmount hook before VMK disposal
- adapter self-tests for nested lookup, enumeration, range reads, and write denial
- tamper and round-trip self-tests

## Important alpha limitations

The following are **not complete**:

- crash-safe transactional storage suitable for production
- recovery keys
- full Windows lock/sleep/sign-out hooks
- independent security review
- verified GitHub Actions build execution

Keep independent backups and use test/disposable data during development.

## Security design

Passwords are not stored. A password-derived KEK protects the random VMK; HKDF-derived subkeys protect metadata and content records.

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
- .NET AES-GCM, HKDF, RNG and constant-time cryptographic helpers
- Konscious.Security.Cryptography.Argon2 for Argon2id

The v0.3 adapter uses DokanNet 2.3.0.3 rather than a custom Rice2k kernel driver. A compatible Dokany 2.x Windows runtime/driver is required to mount the Explorer drive.

## Build

Prerequisites:

- Windows 10/11
- .NET 8 SDK
- Dokany 2.x runtime/driver for Explorer-drive mounting

```powershell
dotnet restore src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
dotnet build src/Rice2k.FolderVault/Rice2k.FolderVault.csproj -c Release
dotnet run --project src/Rice2k.FolderVault/Rice2k.FolderVault.csproj
```

Run the crypto/storage self-test:

```powershell
dotnet run --project tests/Rice2k.FolderVault.CryptoSelfTest/Rice2k.FolderVault.CryptoSelfTest.csproj -c Release
```

## Versioning

Application versions use semantic versioning. The vault-format version is tracked separately so an application update does not automatically make an existing vault incompatible.

See [CHANGELOG.md](CHANGELOG.md) and [ROADMAP.md](ROADMAP.md).

## Author

A Rice2k project.
