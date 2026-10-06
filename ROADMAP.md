# Rice2k Folder Vault Roadmap

## v0.1.x — Application foundation

- [x] Repository and versioning structure
- [x] WPF Windows shell
- [x] Tray-icon foundation
- [x] Unlock/lock UI state
- [x] Settings model
- [x] Auto-lock timer foundation
- [ ] Final application icon assets
- [ ] Persist settings safely
- [ ] Structured diagnostics with sensitive-data redaction

## v0.2.x — Vault format and encrypted storage

- [ ] Freeze the vault header format for stable compatibility
- [x] Define versioned v1-alpha `.rvault` header
- [x] Generate a random 256-bit Vault Master Key
- [x] Password-based key derivation using Argon2id
- [x] Encrypt/wrap the VMK with AES-256-GCM
- [x] HKDF-SHA256 subkey derivation
- [x] Encrypted/authenticated vault metadata segment
- [x] Chunked authenticated file-content encryption
- [x] Persist FILE records inside the vault container
- [x] Update encrypted metadata during file import
- [x] Export/decrypt stored FILE records by content-record ID
- [x] Alpha encrypted Vault Contents manager
- [x] Password change engine by VMK re-wrap
- [ ] Password change UI
- [ ] Optional recovery-key wrapping slot
- [x] Header/metadata/content tamper checks
- [x] Persistent import/export self-tests
- [ ] File rename
- [ ] File deletion and record compaction
- [ ] Nested directories
- [ ] Crash-safe transactional commit/journal design
- [ ] Verify CI execution once GitHub-hosted runner issue is resolved

## v0.3.x — Windows filesystem integration

- [ ] Select and document Windows virtual-filesystem dependency
- [ ] Integrate a proven Windows virtual-filesystem layer
- [ ] Mount unlocked vault as a Windows drive
- [ ] Explorer drag/drop, copy, rename, folders, delete
- [x] Chunked read/write streaming encryption primitive
- [x] Connect streaming encryption to persistent vault records
- [ ] Random-access content IO suitable for filesystem callbacks
- [ ] Open-file tracking
- [ ] Safe flush and unmount
- [ ] Read-only recovery mount

## v0.4.x — Auto-lock and Windows lifecycle

- [ ] True user-idle detection
- [ ] Lock when Windows locks
- [ ] Lock on sign-out
- [ ] Lock before/after sleep or hibernate as appropriate
- [ ] Lock during shutdown/restart
- [ ] Configurable warning countdown
- [ ] Safe behavior when files are still open
- [ ] Emergency Lock Now command/hotkey

## v0.5.x — Recovery and usability

- [ ] Recovery-key creation
- [ ] Print/save recovery-key workflow
- [ ] Vault health check
- [ ] Backup/export workflow
- [ ] Multiple-vault management UI
- [ ] Import/register an existing `.rvault`
- [ ] Portable/removable-drive workflow
- [ ] Custom per-vault icons and names

## v0.6.x — Windows Hello

- [ ] Optional Windows Hello convenience unlock
- [ ] Secure credential/key wrapping through supported Windows APIs
- [ ] Fallback to master password/recovery key

## v0.9.x — Hardening

- [ ] Independent security review
- [ ] Crash-recovery testing
- [ ] Abrupt power-loss testing
- [ ] Large-file tests
- [ ] Concurrent file-operation tests
- [ ] Upgrade/migration tests
- [ ] Installer/uninstall behavior
- [ ] Signed release pipeline

## v1.0.0 — Stable

A release will not be called 1.0 until the encrypted format, filesystem layer, lock/unlock lifecycle, recovery behavior, transactional integrity, and upgrade path are considered stable.
