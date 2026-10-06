# Rice2k Folder Vault

**Rice2k Folder Vault** is a Windows encrypted-vault application designed to make protected files feel as easy to use as a normal folder while keeping stored data encrypted whenever the vault is locked.

> Current status: **v0.2.2-alpha — encrypted file maintenance working; Explorer drive mounting is next**

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
4. Open **Vault Contents**.
5. Add files; they are streamed into authenticated encrypted `FILE` records.
6. Protected filenames and file metadata are stored in the encrypted `META` segment.
7. Export a selected file when plaintext access is required.
8. Lock the vault to destroy the active in-memory master-key session.

The future v0.3 filesystem layer will replace the temporary Vault Contents workflow with a normal Explorer drive such as `V:\`.

## What v0.2.2-alpha implements

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
- tamper and round-trip self-tests

## Important alpha limitations

The following are **not complete**:

- Explorer virtual-drive mounting
- directory creation and nested folders in the UI
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

A proven Windows user-mode filesystem layer will be used for v0.3 rather than writing a kernel filesystem driver.

## Build

Prerequisites:

- Windows 10/11
- .NET 8 SDK

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
