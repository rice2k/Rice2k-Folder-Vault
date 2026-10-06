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

- [ ] Define and freeze vault header format
- [ ] Generate a random 256-bit vault master key
- [ ] Password-based key derivation using Argon2id
- [ ] Encrypt/wrap master key with the derived key
- [ ] Authenticated encryption for vault metadata and contents
- [ ] Password change by re-wrapping the master key instead of re-encrypting all user data
- [ ] Optional recovery-key wrapping slot
- [ ] Corruption/tamper detection
- [ ] Automated cryptographic format tests

## v0.3.x — Windows filesystem integration

- [ ] Integrate a proven Windows virtual-filesystem layer
- [ ] Mount unlocked vault as a Windows drive
- [ ] Explorer drag/drop, copy, rename, folders, delete
- [ ] Read/write streaming encryption
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
- [ ] Multiple vaults
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
