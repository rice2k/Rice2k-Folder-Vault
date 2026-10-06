# Architecture

## Overview

Rice2k Folder Vault separates presentation, Windows integration, key management, encrypted storage, and the future filesystem layer.

```text
+--------------------------------------------------+
|                  WPF Desktop UI                  |
| main window | unlock | settings | vault manager |
+---------------------------+----------------------+
                            |
+---------------------------v----------------------+
|              Application / Vault Controller      |
| state | policies | timers | lifecycle decisions |
+----------+----------------+----------------------+
           |                |
+----------v---------+ +----v----------------------+
| Windows Integration| | Key Management            |
| tray/session/power | | Argon2id / VMK / HKDF     |
+----------+---------+ +-------------+-------------+
           |                         |
           +-------------+-----------+
                         |
+------------------------v--------------------------+
|            Encrypted Vault Storage Engine         |
| header | metadata | chunked content | recovery   |
+------------------------+--------------------------+
                         |
+------------------------v--------------------------+
|          Virtual Filesystem / Mount Layer         |
| Explorer-visible drive while vault is unlocked   |
+---------------------------------------------------+
```

## Current implementation — v0.2.0-alpha

Implemented:

- WPF desktop shell and tray foundation
- local registry of known vault-container paths
- `.rvault` creation UI
- versioned vault header
- random 256-bit Vault Master Key
- Argon2id password key derivation
- AES-256-GCM master-key wrapping
- authenticated encrypted metadata segment
- HKDF-SHA256 metadata subkey
- chunked streaming file-content encryption service
- HKDF-SHA256 per-content-record keys
- password change by master-key re-wrap
- session-owned VMK cleared on lock
- crypto self-test project

Still pending:

- persistent FILE-record append/rewrite integration
- metadata transaction/compaction layer
- virtual filesystem mount
- complete Windows lifecycle hooks
- recovery key
- production security review

## UI

Responsibilities:

- display state
- collect explicit user commands
- show security warnings
- expose settings
- never retain raw master-key material

## Vault controller

Responsibilities:

- own the locked/unlocked state machine
- coordinate unlock and lock sequences
- enforce policy
- coordinate filesystem mount/unmount
- coordinate Windows lifecycle events

## Key management

Responsibilities:

- create the random Vault Master Key
- derive password KEKs using Argon2id
- wrap/unwrap the VMK with AES-GCM
- derive domain-separated subkeys using HKDF-SHA256
- manage future recovery-key slots
- minimize secret lifetime in memory

## Storage engine

Responsibilities:

- parse/write versioned vault headers
- authenticate encrypted metadata
- stream encrypted file chunks
- maintain record/index metadata
- commit changes crash-safely
- validate integrity before exposing data
- migrate supported vault-format versions explicitly

## Filesystem adapter

Responsibilities:

- expose the unlocked vault through normal Windows file operations
- map Explorer operations to encrypted storage
- track open handles
- flush encrypted writes before unmounting
- support safe read-only recovery when required

## State machine

```text
Locked
  |
  | password -> Argon2id -> authenticated VMK unwrap
  v
Unlocking
  |
  | metadata authentication + future mount
  v
Unlocked
  |
  | manual/policy/system event
  v
Locking
  |
  | flush + future unmount + VMK disposal
  v
Locked
```

Failure during **Unlocking** returns to **Locked**.

Failure during future **Locking** must be surfaced clearly; the UI must never display "Locked" while a usable mount remains active.

## Dependency rule

Security-sensitive implementation uses platform/reviewed primitives. Security-critical dependencies must have a documented reason.

Current external crypto dependency:

- `Konscious.Security.Cryptography.Argon2` — Argon2id password derivation

AES-GCM, HKDF, RNG, constant-time comparison, and memory-zero helpers use .NET cryptography APIs.

## Logging boundary

Logs may contain:

- application version
- non-sensitive error category
- state-transition type
- elapsed durations
- operating-system compatibility information

Logs must not contain:

- passwords
- recovery keys
- encryption keys
- decrypted vault metadata
- protected filenames
- protected file contents

## Version compatibility

Application version and vault-format version are separate. Incompatible vault-format changes require explicit versioning and migration behavior.

See [VAULT-FORMAT.md](VAULT-FORMAT.md).
