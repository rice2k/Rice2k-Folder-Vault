# Security Design

> Status: design document for the pre-production alpha. The production vault format is not implemented yet.

## Security objective

When a vault is **locked**, obtaining the vault files or storage device should not reveal protected file contents, filenames, directory names, or protected metadata without a valid unlock credential.

## Important boundary

No desktop vault can guarantee secrecy against malware, an administrator/kernel-level attacker, memory inspection, screen/key capture, or another process that already has equivalent access **while the vault is unlocked**.

The primary security boundary is therefore the locked state.

## Key hierarchy

Planned model:

1. Generate a random 256-bit **Vault Master Key (VMK)**.
2. Generate a unique random salt for password derivation.
3. Derive a **Key Encryption Key (KEK)** from the password using **Argon2id**.
4. Use authenticated encryption to wrap the VMK with the KEK.
5. Use keys derived from the VMK to protect vault metadata and file data.
6. Never store the plaintext password.

Changing a password should normally re-derive a KEK and re-wrap the VMK. It should not require decrypting and re-encrypting every file.

## Authenticated encryption

The production design should use an established authenticated-encryption primitive such as **AES-256-GCM**, with unique nonces and strict nonce-management rules.

Cryptographic choices must be implemented through maintained, reviewed libraries/platform primitives rather than custom cipher code.

## Metadata confidentiality

While locked, the format should protect at minimum:

- file names
- directory names
- file contents
- logical sizes where practical
- timestamps where practical
- internal directory structure

Some outer-container information will necessarily remain visible, such as container existence and approximate physical size.

## Recovery key

If enabled, a recovery key is another independent wrapping credential for the same VMK.

The application must clearly tell the user:

- anyone with the recovery key can unlock the vault
- the recovery key should be kept somewhere separate
- losing both password and recovery key can make the vault permanently inaccessible

## Memory handling

Sensitive key material should:

- exist in memory only when needed
- be cleared as soon as practical after lock
- not be written to logs
- not be serialized into crash reports
- avoid immutable string representations when possible

.NET does not provide absolute guarantees that managed-memory remnants are unrecoverable, so the implementation should minimize lifetime and copies of secret material.

## Password guessing

The design must use a memory-hard KDF with per-vault salt. Parameters will be benchmarked for modern Windows systems and encoded into the vault header so they can evolve.

The application may add increasing UI delays after repeated failed attempts, but the cryptographic KDF is the primary defense against offline guessing.

## Vault header

The future vault header should include non-secret format data such as:

- magic/version identifier
- vault-format version
- KDF identifier and parameters
- KDF salt
- wrapped VMK slots
- cipher-suite identifier
- integrity/authentication data

The header must be versioned from the beginning.

## Filesystem mount

The project should integrate a proven Windows user-mode filesystem layer. The filesystem layer is an access mechanism, not the security boundary; encrypted storage remains the source of truth.

## Lock sequence

A safe lock operation should:

1. stop accepting new writes
2. request applications to close/finish active file access as policy allows
3. flush encrypted writes
4. commit authenticated metadata
5. unmount the virtual filesystem
6. clear active key material as far as practical
7. change UI/tray state to locked

## Backups

Encryption does not protect against deletion, corruption, drive failure, or ransomware. Vault users still need backups.

## Release rule

No build should be described as secure or production-ready until the implemented vault format and key lifecycle have been tested beyond the UI prototype stage.
