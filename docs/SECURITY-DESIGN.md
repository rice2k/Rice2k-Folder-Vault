# Security Design

> Status: **pre-production v0.2 alpha implementation.** Core key wrapping, metadata encryption, and streaming content-encryption primitives now exist, but the complete persistent storage/mount lifecycle has not been security-reviewed.

## Security objective

When a vault is **locked**, obtaining the vault file or storage device should not reveal protected file contents, filenames, directory names, or protected metadata without a valid unlock credential.

## Important boundary

No desktop vault can guarantee secrecy against malware, an administrator/kernel-level attacker, memory inspection, screen/key capture, or another process with equivalent access while the vault is unlocked.

The primary security boundary is therefore the locked state.

## Implemented key hierarchy

1. Generate a random 256-bit **Vault Master Key (VMK)**.
2. Generate a random 256-bit Argon2id salt.
3. Derive a 256-bit **Key Encryption Key (KEK)** from the password using Argon2id.
4. Wrap/authenticate the VMK using AES-256-GCM.
5. Derive domain-separated metadata/content subkeys from the VMK using HKDF-SHA256.
6. Keep the VMK only in the unlocked in-memory session and zero its managed byte buffer on lock.
7. Never store the plaintext password.

Changing the password re-derives a KEK and re-wraps the same VMK. It does not require re-encrypting payload data.

## Current KDF parameters

The current v1-alpha defaults are:

- Argon2id
- 65,536 KiB memory
- 4 iterations
- parallelism 2
- 256-bit random per-vault salt

Parameters are stored in the vault header so later releases can evolve them.

## Authenticated encryption

Current primitives:

- AES-256-GCM for VMK wrapping
- AES-256-GCM for metadata
- AES-256-GCM for chunked file-content records
- 96-bit random nonces
- 128-bit authentication tags

Header, metadata, and file-chunk contexts are bound through associated authenticated data.

## Metadata confidentiality

The metadata segment is encrypted. Its model includes protected names, hierarchy, logical file sizes, timestamps, and content-record identifiers.

Outer-container information still leaks unavoidable facts such as:

- a vault file exists
- approximate physical size
- format/KDF identifiers and parameters

## File-content encryption

The content service encrypts files in independent chunks (1 MiB default). Each content record receives a domain-separated HKDF-derived key and each chunk has an independent AES-GCM nonce/tag.

Associated data binds each chunk to:

- vault ID
- content-record ID
- chunk index
- chunk length
- full plaintext file length

## Recovery key

Not implemented yet.

When added, recovery must be another independent VMK-wrapping credential, never a vendor backdoor.

## Memory handling

Sensitive key material should:

- exist only while needed
- be cleared after lock/operation where practical
- never be logged
- never be serialized into diagnostics

The current implementation uses `CryptographicOperations.ZeroMemory` for managed key/plaintext buffers where practical. .NET cannot guarantee that all historical managed-memory copies are unrecoverable, especially password strings produced by WPF controls.

## Password guessing

Argon2id is the primary defense against offline password guessing. UI delay/backoff may be added later but cannot replace a strong memory-hard KDF.

## Vault header

The implemented v1-alpha header contains:

- magic/version identity
- format version
- vault ID
- KDF identifier/parameters/salt
- cipher-suite identifier
- AES-GCM wrapped VMK
- nonce/tag

The wrapped VMK uses authenticated associated data covering the security-relevant header parameters.

See [VAULT-FORMAT.md](VAULT-FORMAT.md).

## Lock sequence

Current alpha lock behavior destroys the in-memory VMK session object.

The final lock sequence must:

1. stop new writes
2. handle open file handles according to policy
3. flush encrypted writes
4. commit authenticated metadata
5. unmount the virtual filesystem
6. clear active key material
7. only then show the vault as locked

## Tamper detection

The self-test exercises:

- incorrect password rejection
- wrapped-VMK tag tampering
- encrypted metadata tampering
- chunked file-content tampering
- password re-wrap while preserving encrypted metadata
- empty-file and multi-chunk content round trips

## Backups

Encryption does not protect against deletion, corruption, storage failure, or ransomware. Vaults still require independent backups.

## Release rule

No build should be described as production-ready until persistent storage integration, filesystem mounting, recovery behavior, crash testing, and security review are complete.
