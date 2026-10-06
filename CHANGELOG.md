# Changelog

All notable changes to **Rice2k Folder Vault** will be documented here.

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
