# Changelog

All notable changes to **Rice2k Folder Vault** will be documented here.

## [0.3.0-alpha] - 2026-10-06

### Added

- Read-only DokanNet filesystem adapter implementing `IDokanOperations`.
- Read-only Windows Explorer mount lifecycle through `DokanInstanceBuilder`.
- Dokan mount options for write protection, Windows Mount Manager, and current-session mounting.
- Persistent authenticated random-access file reads by encrypted entry ID.
- Explorer enumeration of encrypted root and nested directory metadata.
- File size/timestamp reporting to Windows Explorer.
- Main-window **Mount Explorer Drive** and **Unmount Drive** controls.
- System-tray mount/unmount controls.
- Preferred Explorer drive-letter setting.
- Optional mount-and-open-Explorer behavior after successful unlock.
- Pre-lock unmount hook so mounted access is removed before VMK disposal.
- Filesystem adapter self-tests using DokanNet's mock file-info implementation.

### Changed

- Application version advanced to `0.3.0-alpha`.
- Automatic Explorer mounting is opt-in during alpha development.
- Mount-state UI callbacks now use asynchronous dispatcher updates to avoid unmount/UI deadlocks.
- The Explorer filesystem is deliberately read-only until transactional encrypted writes are designed and tested.

### Security

- Mounted reads decrypt only requested authenticated FILE ranges; no plaintext staging file is created for virtual-drive reads.
- Create, write, delete, rename, truncate, allocation-size, attribute-change, and security-change filesystem callbacks are denied.
- Dokan write protection is enabled in addition to application-level write denial.
- Lock always proceeds to VMK destruction even if best-effort pre-lock unmount cleanup throws.
- Runtime mount verification on a Windows machine with Dokany 2.x installed is still required before treating this milestone as build/runtime verified.

## [0.2.3-alpha] - 2026-10-06

### Added

- Encrypted nested directory entries using the existing authenticated META structure.
- Create-folder support inside any vault directory.
- Folder navigation with current encrypted path display and Up navigation.
- Double-click folder navigation in Vault Contents.
- Import files directly into the currently open encrypted directory.
- Export files by internal entry ID instead of relying on root-level filename lookup.
- Generic rename support for both encrypted files and directories.
- Recursive encrypted directory deletion with FILE-record compaction.
- Reusable vault-item naming dialog.
- Self-tests for nested path resolution, nested import/export, same-name files in separate folders, folder rename, safe non-recursive delete rejection, and recursive delete/compaction.

### Changed

- Vault Contents now presents both folders and files.
- Folder hierarchy remains inside encrypted/authenticated metadata; no plaintext sidecar index is introduced.
- Version advanced to `0.2.3-alpha`.

### Security

- Directory names and parent/child relationships are protected by the existing encrypted META segment.
- Same filenames may exist safely in different encrypted directories because operations use internal entry IDs.
- Recursive directory deletion removes descendant metadata and compacts descendant encrypted FILE records.
- The vault format remains v1-alpha; this milestone does not require a format-version bump.

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
