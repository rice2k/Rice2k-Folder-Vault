# Changelog

All notable changes to **Rice2k Folder Vault** will be documented here.

## [0.2.0-alpha] - 2026-10-05

### Added

- Portable per-vault `.rvault` container creation.
- Versioned `R2FVLT01` vault header.
- Random 256-bit Vault Master Key per vault.
- Argon2id password-to-KEK derivation using a per-vault 256-bit salt.
- AES-256-GCM authenticated wrapping of the VMK.
- Per-vault local registry storing display name, container path, and non-secret vault ID.
- Encrypted/authenticated META segment for protected directory/file metadata.
- HKDF-SHA256 domain-separated metadata key derivation.
- Chunked streaming file-content encryption service using AES-256-GCM.
- HKDF-SHA256 per-content-record key derivation.
- 1 MiB default encrypted file chunks with per-chunk nonce/tag and ordering/length binding.
- Password change by re-wrapping the existing VMK.
- Dependency-light crypto self-test project.
- Tests for wrong passwords, header tampering, metadata tampering, file-content tampering, password re-wrap, empty files, and multi-chunk round trips.
- Formal `.rvault` format documentation.

### Changed

- Removed the temporary Local AppData password-verifier prototype.
- Unlock now authenticates the selected `.rvault` header and encrypted metadata.
- The active VMK is owned by the unlocked vault session and disposed/cleared on lock.
- First-run flow now creates an actual `.rvault` container.
- Main application UI now reports the v0.2 vault-engine state.

### Security

- Protected metadata is no longer represented as plaintext outside the encrypted META segment.
- Security-relevant header fields are authenticated as AES-GCM associated data.
- File chunks are bound to vault ID, content-record ID, index, chunk length, and total plaintext length.
- Alpha status remains: persistent FILE-record integration, Explorer mounting, recovery slots, crash-hardening, and independent security review are still pending.

## [0.1.1-alpha] - 2026-10-05

### Added

- First-run password-verification prototype using Argon2id.
- Minimum 12-character password requirement.

### Changed

- Replaced preview unlock behavior with actual password verification.
- This prototype credential model was superseded by the per-vault v0.2 design.

## [0.1.0-alpha] - 2026-10-05

### Added

- Initial repository structure.
- C# / .NET 8 / WPF application foundation.
- Locked/unlocked vault-state prototype.
- Unlock prompt and main control panel.
- Settings and auto-lock preference model.
- Windows system-tray foundation.
- Product, security, UI, architecture, and roadmap documentation.
