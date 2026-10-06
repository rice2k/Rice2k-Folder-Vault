# Changelog

All notable changes to **Rice2k Folder Vault** will be documented here.

## [0.2.2-alpha] - 2026-10-06

### Added

- Root-level encrypted file rename support.
- Root-level file deletion support.
- Encrypted record compaction during delete.
- Manual **Compact Vault** operation for removing unreferenced encrypted FILE records.
- Dedicated Rename File dialog in the Vault Contents window.
- Vault Contents controls for Rename, Delete, and Compact Vault.
- Self-tests covering rename, old-name rejection, deletion, retained-file integrity, and compaction.

### Changed

- Vault Contents now supports basic encrypted file maintenance in addition to import/export.
- Version advanced to `0.2.2-alpha`.

### Security

- Rename updates authenticated encrypted metadata without decrypting or rewriting the protected file payload.
- Delete rewrites the container and excludes encrypted FILE records no longer referenced by metadata.
- Compaction preserves referenced encrypted FILE records byte-for-byte while dropping unreferenced records.
- These operations still use the alpha replacement-container rewrite model and do not yet provide formal crash/power-loss transaction guarantees.

## [0.2.1-alpha] - 2026-10-05

### Added

- Persistent encrypted `FILE` records inside `.rvault` containers.
- Atomic-style encrypted container rewrite workflow for imports.
- Encrypted metadata updates for imported files.
- Root-level duplicate filename protection.
- Encrypted record scanning by content-record ID.
- File export/decryption from persistent vault records.
- Alpha **Vault Contents** window with:
  - Add Files
  - Export Selected
  - Refresh
  - encrypted metadata listing
- Main-window and system-tray access to Vault Contents while unlocked.
- End-to-end self-tests for persistent import/export.
- Stored file-record tamper test.
- Verification that encrypted file payload remains readable after password re-wrap.
- Change Vault Password dialog available from the main window and system tray.

### Changed

- Main UI now reports encrypted file storage as active.
- Documentation now distinguishes implemented encrypted storage from the still-pending Explorer filesystem mount.
- Version advanced to `0.2.1-alpha`.

### Security

- Imports do not create a plaintext staging copy; source data is streamed directly into an encrypted temporary FILE record.
- Container rewrite temporary files contain encrypted data.
- Exports intentionally create plaintext output and use a temporary partial output before finalizing.
- A vault may unlock successfully when an unused FILE record is corrupted because each FILE record is authenticated when accessed; corrupted referenced content is rejected during export.
- Persistent storage is still alpha: crash-safe journaling/transactions, compaction, recovery-key support, filesystem mounting, and independent review remain pending.

## [0.2.0-alpha] - 2026-10-05

### Added

- Portable per-vault `.rvault` container creation.
- Versioned `R2FVLT01` vault header.
- Random 256-bit Vault Master Key.
- Argon2id password-to-KEK derivation.
- AES-256-GCM authenticated VMK wrapping.
- Encrypted/authenticated META segment.
- HKDF-SHA256 metadata and content subkeys.
- Chunked AES-256-GCM file-content service.
- Password change by re-wrapping the VMK.
- Cryptographic self-test foundation.
- Formal vault-format documentation.

### Changed

- Replaced the temporary Local AppData password verifier with per-vault credentials stored in each vault header.
- Unlock now authenticates the vault header and encrypted metadata.
- The active VMK is session-owned and cleared on lock.

## [0.1.1-alpha] - 2026-10-05

- Added the temporary Argon2id password-verification prototype later superseded by the per-vault v0.2 design.

## [0.1.0-alpha] - 2026-10-05

- Initial C#/.NET 8/WPF application and repository foundation.
