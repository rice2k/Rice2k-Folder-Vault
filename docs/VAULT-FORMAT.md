# Rice2k Folder Vault Format

> Status: **v1 alpha format for application v0.2.x.** The format is versioned but is **not frozen for stable releases yet**.

## Container layout

A `.rvault` file currently uses this outer layout:

```text
+------------------------------+
| 8 bytes  Magic: R2FVLT01     |
+------------------------------+
| 4 bytes  Header length (LE)  |
+------------------------------+
| N bytes  UTF-8 JSON header   |
+------------------------------+
| META encrypted segment       |
+------------------------------+
| FILE encrypted records       |  <- service implemented; container integration pending
| ...                          |
+------------------------------+
```

The outer header intentionally contains only information needed to derive the password key and unwrap the Vault Master Key. Protected filenames, directory names, and user content must not appear there.

## Header fields

The current header contains:

- format version
- random 128-bit vault identifier
- cipher-suite identifier
- KDF identifier
- Argon2id parameters
- random 256-bit KDF salt
- AES-GCM nonce for the wrapped Vault Master Key
- wrapped 256-bit Vault Master Key ciphertext
- AES-GCM authentication tag

Current identifiers:

- format version: `1`
- cipher suite: `AES-256-GCM`
- KDF: `argon2id`

Current default Argon2id settings:

- memory: 65,536 KiB
- iterations: 4
- parallelism: 2

These parameters are stored per vault so later versions can evolve them.

## Key hierarchy

```text
User password
     |
     v
  Argon2id + per-vault salt
     |
     v
Key Encryption Key (KEK)
     |
     | AES-256-GCM unwrap
     v
Vault Master Key (VMK) -- random 256 bits
     |
     +--> HKDF-SHA256 metadata key
     |
     +--> HKDF-SHA256 per-file content key
```

Changing a password generates a new KDF salt and KEK, then re-wraps the same VMK. Encrypted metadata and future encrypted file records do not need to be re-encrypted merely because the password changes.

## Header authentication

The wrapped VMK uses AES-256-GCM.

Associated data binds the wrapped key to:

- Rice2k Folder Vault format identity
- format version
- vault identifier
- cipher-suite identifier
- KDF identifier
- KDF memory setting
- KDF iteration setting
- KDF parallelism setting
- KDF salt

Changing any of those fields without the correct encryption key causes master-key authentication to fail.

## META segment

Every newly-created v0.2 vault contains one encrypted metadata segment.

Layout:

```text
4 bytes   "META"
4 bytes   segment version (LE)
4 bytes   ciphertext length (LE)
12 bytes  AES-GCM nonce
16 bytes  AES-GCM tag
N bytes   encrypted metadata JSON
```

The metadata encryption key is derived from the VMK with HKDF-SHA256 and vault-specific context.

Protected metadata currently models:

- metadata revision
- root directory identifier
- file/directory entry identifiers
- parent directory identifiers
- filenames
- entry type
- plaintext file length
- last-write timestamp
- encrypted content-record identifier

A new vault starts with an encrypted empty metadata tree.

## FILE record

The content encryption service implements a streaming file-record format for future container integration.

Record header:

```text
4 bytes   "FILE"
4 bytes   record version (LE)
16 bytes  random content-record identifier
4 bytes   chunk size (LE)
8 bytes   total plaintext length (LE)
4 bytes   chunk count (LE)
```

Each encrypted chunk contains:

```text
4 bytes   chunk index (LE)
4 bytes   plaintext chunk length (LE)
12 bytes  AES-GCM nonce
16 bytes  AES-GCM tag
N bytes   ciphertext
```

Default chunk size is 1 MiB.

Each file record receives its own HKDF-SHA256-derived content key. Chunk authentication binds the ciphertext to the vault ID, content-record ID, chunk index, chunk length, and total plaintext file length.

This prevents undetected chunk modification or reordering.

## Password change

Password changes use an atomic temporary-container rewrite:

1. authenticate and unwrap the existing VMK
2. authenticate the encrypted metadata
3. derive a new password KEK using a new salt
4. wrap the same VMK with the new KEK
5. write the replacement header
6. copy the encrypted payload unchanged
7. replace the old container

## Current implementation boundary

Implemented in v0.2.0-alpha:

- versioned `.rvault` header
- Argon2id password KDF
- random 256-bit VMK
- AES-256-GCM VMK wrapping
- encrypted/authenticated metadata segment
- HKDF-SHA256 metadata subkey
- chunked AES-256-GCM file-content service
- HKDF-SHA256 per-file subkeys
- password re-wrap
- header and metadata tamper checks
- dependency-light crypto self-test project

Not yet implemented:

- adding FILE records to the persistent container from the application UI
- metadata updates for imported files
- record indexing/compaction
- crash-safe transactional file updates beyond header rewrite
- recovery-key slots
- Explorer virtual-drive mounting
- production security review

## Compatibility rule

Application version and vault-format version are separate.

Stable releases must not silently reinterpret existing format fields. Any future incompatible vault-format change requires a new explicit format version and a tested migration/recovery path.
