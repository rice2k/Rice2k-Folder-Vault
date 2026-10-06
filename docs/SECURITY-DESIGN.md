# Security Design

> Status: **pre-production v0.2.1 alpha.** Header, metadata and persistent file records are encrypted/authenticated, but filesystem mounting, transactional crash safety, recovery, and independent review are incomplete.

## Security objective

While a vault is locked, possession of the `.rvault` file should not expose protected filenames, directory structure, metadata, or file contents without a valid unlock credential.

## Important boundary

An unlocked desktop vault cannot guarantee secrecy from malware, administrator/kernel-level attackers, memory inspection, keylogging, screen capture, or another process with equivalent access. The primary cryptographic boundary is the locked state.

## Implemented key hierarchy

1. random 256-bit VMK
2. random 256-bit Argon2id salt
3. password -> Argon2id -> 256-bit KEK
4. KEK -> AES-256-GCM wrap/authenticate VMK
5. VMK -> HKDF-SHA256 domain-separated metadata/content keys
6. VMK retained only for the unlocked application session
7. session VMK buffer zeroed on lock where practical

Passwords are not stored.

## Current Argon2id defaults

- memory: 65,536 KiB
- iterations: 4
- parallelism: 2
- salt: 256 random bits

Parameters are stored in the vault header to permit future upgrades.

## Authenticated encryption

Current primitives:

- AES-256-GCM for VMK wrapping
- AES-256-GCM for META
- AES-256-GCM for each FILE chunk
- 96-bit random nonces
- 128-bit authentication tags
- HKDF-SHA256 for domain-separated VMK subkeys

Associated data binds security-relevant context to each ciphertext.

## Persistent file storage

Imports stream plaintext from the selected source directly into an encrypted temporary FILE record. The container is then rewritten using encrypted META/FILE data.

The import process does not intentionally create a plaintext staging copy.

Exports intentionally create plaintext because exporting is an explicit request to take a file out of the vault. A partial plaintext file may exist while export is in progress; best-effort cleanup is performed on failure.

## Metadata confidentiality

Protected metadata includes names, hierarchy identifiers, logical file lengths, timestamps, and FILE record identifiers. It is encrypted in META.

Visible outer information includes the vault's existence, approximate total size, format version, KDF/cipher identifiers, and non-secret KDF parameters.

## Authentication scope and corruption detection

Unlock authenticates the wrapped VMK and META.

FILE records are authenticated on access. This keeps unlock cost independent of total stored file content. A health-check feature should later scan every FILE record to proactively find corruption.

The self-test currently exercises:

- wrong-password rejection
- wrapped-VMK authentication-tag tampering
- META tampering
- standalone FILE tampering
- persistent stored FILE tampering
- multi-chunk round trip
- empty-file round trip
- persistent import/export
- duplicate-name rejection
- password re-wrap with content preserved

## Memory limitations

The implementation zeroes byte arrays containing VMKs, derived keys, decrypted chunk buffers, and other sensitive transient data where practical.

WPF password controls ultimately expose managed strings for Argon2 processing, and .NET cannot guarantee erasure of every historical managed-memory copy. This limitation must be treated honestly in the threat model.

## Crash/power-loss limitation

The current alpha uses replacement-container rewrites but does not yet implement a formal journal, fsync/rename durability protocol across all supported Windows/storage environments, or automatic rollback.

Do not represent v0.2.x as production-safe storage.

## Recovery

Recovery-key support is not implemented. When added it must be an independent VMK-wrapping credential, not a vendor backdoor.

## Filesystem mount

The future Windows filesystem layer is an access mechanism, not the encryption boundary. A vault must remain encrypted at rest regardless of how it is mounted.

## Backups

Encryption does not prevent deletion, corruption, drive failure, ransomware, or accidental overwrite. Independent backups remain necessary.

## Release rule

Production-ready claims require at minimum filesystem lifecycle completion, crash/power-loss testing, recovery behavior, migration testing, and independent security review.
