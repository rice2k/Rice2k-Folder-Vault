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

## v0.2.x — Vault format and cryptography

- [ ] Freeze the vault header format for stable compatibility
- [x] Define versioned v1-alpha `.rvault` header
- [x] Generate a random 256-bit Vault Master Key
- [x] Password-based key derivation using Argon2id
- [x] Encrypt/wrap the master key with AES-256-GCM
- [x] HKDF-SHA256 subkey derivation from the VMK
- [x] Encrypted/authenticated vault metadata segment
- [x] Chunked authenticated file-content encryption service
- [ ] Persist FILE records and metadata updates inside the container
- [x] Password change by re-wrapping the master key instead of re-encrypting payload data
- [ ] Optional recovery-key wrapping slot
- [x] Header/metadata/content corruption and tamper checks
- [x] Dependency-light cryptographic self-test project
- [ ] Verify CI execution once GitHub-hosted runner issue is resolved

## v0.3.x — Windows filesystem integration

- [ ] Integrate a proven Windows virtual-filesystem layer
- [ ] Mount unlocked vault as a Windows drive
- [ ] Explorer drag/drop, copy, rename, folders, delete
- [x] Chunked read/write streaming encryption primitive
- [ ] Connect streaming encryption to persistent vault records
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
- [ ] Portable/removable-drive vault option
- [ ] Custom per-vault icons and names

## v0.6.x — Windows Hello

- [ ] Optional Windows Hello convenience unlock
- [ ] Secure credential/key wrapping through supported Windows APIs
- [ ] Fallback to master password/recovery key

## v0.9.x — Hardening

- [ ] Security review
- [ ] Crash-recovery testing
- [ ] Abrupt power-loss testing
- [ ] Large-file tests
- [ ] Concurrent file-operation tests
- [ ] Upgrade/migration tests
- [ ] Installer and uninstall behavior
- [ ] Signed release pipeline plan

## v1.0.0 — Stable

A release will not be called 1.0 until the encrypted vault format, filesystem layer, lock/unlock lifecycle, recovery behavior, and upgrade path are considered stable.
