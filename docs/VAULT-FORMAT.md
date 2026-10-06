# Rice2k Folder Vault Format

> Status: **v1 alpha format for application v0.2.x.** It is versioned but not frozen for stable releases.

## Container layout

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
| FILE encrypted record #1     |
+------------------------------+
| FILE encrypted record #2     |
+------------------------------+
| ...                          |
+------------------------------+
```

The header exposes only the information needed to derive the password key and authenticate/unwrap the VMK. Filenames, directory metadata, and file contents are encrypted.

## Header

The v1-alpha header contains:

- format version
- random 128-bit vault ID
- cipher-suite ID
- KDF ID and parameters
- random 256-bit Argon2id salt
- AES-GCM nonce
- wrapped 256-bit VMK ciphertext
- AES-GCM tag

Current identifiers:

- format version: `1`
- cipher: `AES-256-GCM`
- KDF: `argon2id`

Current default Argon2id parameters:

- memory: 65,536 KiB
- iterations: 4
- parallelism: 2

## Key hierarchy

```text
Password
   |
Argon2id + per-vault salt
   |
   v
Key Encryption Key (KEK)
   |
AES-256-GCM unwrap
   |
   v
Random 256-bit Vault Master Key (VMK)
   |
   +--> HKDF-SHA256 metadata key
   |
   +--> HKDF-SHA256 per-content-record key
```

Changing the password creates a new password-derived KEK and re-wraps the same VMK. Existing META and FILE ciphertext remains unchanged.

## META segment

```text
4 bytes   "META"
4 bytes   segment version (LE)
4 bytes   ciphertext length (LE)
12 bytes  AES-GCM nonce
16 bytes  AES-GCM tag
N bytes   encrypted metadata JSON
```

Encrypted metadata models:

- metadata revision
- root directory ID
- entry IDs
- parent directory IDs
- filenames
- file/directory type
- plaintext logical length
- last-write timestamp
- FILE content-record ID

On import, the container is rewritten with a new authenticated META segment and the prior FILE records plus the newly encrypted FILE record.

## FILE records

Each persistent encrypted file record begins with:

```text
4 bytes   "FILE"
4 bytes   record version (LE)
16 bytes  random content-record ID
4 bytes   chunk size (LE)
8 bytes   total plaintext length (LE)
4 bytes   chunk count (LE)
```

Each chunk contains:

```text
4 bytes   chunk index (LE)
4 bytes   plaintext chunk length (LE)
12 bytes  AES-GCM nonce
16 bytes  AES-GCM tag
N bytes   ciphertext
```

Default chunk size is 1 MiB.

Each FILE record gets a domain-separated HKDF-SHA256 content key derived from the VMK. Chunk AAD binds:

- vault ID
- content-record ID
- chunk index
- chunk length
- total plaintext file length

Tampering or reordering is detected when the affected FILE record is read.

## Import

Current v0.2.1 import sequence:

1. stream the source file into an encrypted temporary FILE record
2. open/authenticate the current vault
3. decrypt metadata in memory
4. reject conflicting root filenames
5. add the encrypted FILE record ID and protected file information to metadata
6. write a temporary encrypted container containing:
   - current header
   - newly encrypted META segment
   - existing FILE records
   - new encrypted FILE record
7. flush and replace the original container

No plaintext staging file is created by the import operation.

## Export

Current export sequence:

1. authenticate/decrypt META
2. find the requested protected filename
3. scan FILE record headers until its content-record ID is located
4. authenticate/decrypt chunks into a temporary plaintext export file
5. verify the logical length
6. finalize the requested destination path

The plaintext export and its partial file are intentionally outside the vault security boundary.

## Password change

1. authenticate/unwrap the VMK
2. authenticate META
3. create a new Argon2id salt/KEK
4. wrap the same VMK
5. write a replacement header
6. copy META and FILE ciphertext unchanged
7. replace the original container

## Authentication scope

Unlock authenticates:

- the wrapped VMK/header context
- the META segment

FILE records are individually authenticated when accessed. This avoids decrypting every stored file merely to unlock a large vault. A corrupted unused FILE record can therefore remain undiscovered until a health check or read accesses it.

## Current implementation boundary

Implemented in v0.2.1-alpha:

- versioned header
- Argon2id password KDF
- AES-GCM VMK wrapping
- encrypted META
- persistent encrypted FILE records
- encrypted import
- authenticated export
- password re-wrap
- tamper tests
- alpha Vault Contents UI

Not yet implemented:

- rename/delete/compaction
- nested directories in the UI
- random-access filesystem callbacks
- crash-safe journal/transaction format
- recovery-key slots
- Explorer virtual-drive mount
- stable-format freeze
- production security review

## Compatibility rule

Application version and vault-format version are separate. Incompatible vault-format changes require a new explicit format version and tested migration/recovery behavior.
