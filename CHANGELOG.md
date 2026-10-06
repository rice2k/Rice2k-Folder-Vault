# Changelog

All notable changes to **Rice2k Folder Vault** will be documented here.

## [0.1.1-alpha] - 2026-10-05

### Added

- First-run vault password setup screen.
- Argon2id password verification using a unique 256-bit random salt.
- Constant-time comparison of password verifiers.
- Local credential record stored under the user's Local AppData profile.
- Minimum 12-character password requirement.
- Startup protection: a vault password must be configured before the main application opens.

### Changed

- Unlocking now requires the correct configured password instead of accepting any non-empty password.
- Unlock and main-window text now clearly distinguish password verification from the not-yet-implemented encrypted storage engine.
- Application version advanced to 0.1.1-alpha.

### Security

- Plaintext passwords are never written to the credential record.
- Temporary derived verifier and salt byte arrays are cleared after use where practical.
- Password verification uses Argon2id with 64 MiB memory, 4 iterations, and parallelism of 2.
- This remains an **alpha** and must not yet be used to protect sensitive files because encrypted storage and virtual-drive mounting are not implemented.

## [0.1.0-alpha] - 2026-10-05

### Added

- Initial Rice2k Folder Vault repository structure.
- C# / .NET 8 / WPF application foundation.
- Locked and unlocked vault-state prototype.
- Unlock prompt UI prototype.
- Main vault control panel.
- Settings window and auto-lock preference model.
- Windows system-tray integration foundation.
- Auto-lock countdown foundation.
- Product specification and security architecture documentation.
- GitHub Actions Windows build validation.

### Security

- Documented the intended master-key / password-derived-key architecture.
- Established the rule that passwords, encryption keys, filenames, and file contents must never be written to application logs.
- Marked the alpha shell as **not suitable for protecting sensitive data** until the encrypted storage engine is implemented and reviewed.
