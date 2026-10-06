# Architecture

## Overview

Rice2k Folder Vault separates the WPF interface, Windows lifecycle integration, key management, encrypted storage, and the future Explorer filesystem adapter.

```text
+--------------------------------------------------+
|                  WPF Desktop UI                  |
| unlock | settings | vault contents | manager     |
+---------------------------+----------------------+
                            |
+---------------------------v----------------------+
|              Application / Vault Controller      |
| session | policy | timer | lifecycle decisions  |
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
| header | META | persistent FILE records          |
+------------------------+--------------------------+
                         |
+------------------------v--------------------------+
|          Future Virtual Filesystem Adapter        |
| Explorer-visible drive while vault is unlocked   |
+---------------------------------------------------+
```

## Current implementation — v0.2.2-alpha

Implemented:

- WPF shell and notification-area host
- local registry of known vault paths
- vault creation/unlock workflow
- session-owned VMK lifecycle
- versioned/authenticated header
- Argon2id password derivation
- AES-256-GCM VMK wrapping
- HKDF-SHA256 subkeys
- encrypted META segment
- persistent chunked FILE records
- encrypted import/container rewrite
- authenticated file export
- password re-wrap engine
- Vault Contents alpha UI
- encrypted metadata rename
- delete with encrypted FILE record compaction
- manual orphan-record compaction
- storage/crypto self-tests

Still pending:

- nested directory UI
- crash-safe transaction/journal design
- virtual filesystem mounting
- complete Windows lifecycle hooks
- recovery key
- security review

## Storage write model

The current alpha uses full-container rewrite for metadata-changing imports:

1. encrypt new content to a temporary encrypted record
2. authenticate/read existing META
3. build revised META in memory
4. write a temporary encrypted replacement container
5. copy old FILE records and append the new record
6. flush
7. replace the original container

This is intentionally simple for early correctness. Before production, the design needs stronger crash/power-loss guarantees, rollback/recovery, and compaction.

## Key management

The UI does not own the VMK. `VaultStateService` owns a disposable `VaultSessionKey` while unlocked.

A lock:

- disposes the session key
- zeroes the managed VMK byte buffer
- clears active vault/session state

## File access today

The temporary **Vault Contents** UI calls encrypted storage services directly for import/export.

The future filesystem adapter will translate normal Windows file operations to storage-engine operations. It must not weaken the cryptographic boundary.

## Filesystem adapter requirements

The v0.3 adapter must support:

- normal Explorer enumeration
- open/read/write/create/rename/delete
- random-access IO
- open-handle tracking
- flush semantics
- safe unmount
- lock refusal/warning when handles cannot safely close
- read-only recovery mode

## Logging boundary

Logs may contain application version, non-sensitive error categories, timings, and compatibility information.

They must not contain passwords, recovery keys, VMKs, derived keys, protected names, decrypted metadata, or file contents.

## Compatibility

Application and vault-format versions are separate. Format migrations must be explicit and recoverable.

See [VAULT-FORMAT.md](VAULT-FORMAT.md).
