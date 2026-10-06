# Architecture

## Overview

Rice2k Folder Vault is divided into layers so that presentation code, Windows integration, cryptography, and encrypted storage do not become one large component.

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
| tray/session/power | | password/KDF/key wrapping |
+----------+---------+ +-------------+-------------+
           |                         |
           +-------------+-----------+
                         |
+------------------------v--------------------------+
|            Encrypted Vault Storage Engine         |
| header | metadata | encrypted blocks | recovery  |
+------------------------+--------------------------+
                         |
+------------------------v--------------------------+
|          Virtual Filesystem / Mount Layer         |
| Explorer-visible drive while vault is unlocked   |
+---------------------------------------------------+
```

## Current implementation

Version **0.1.0-alpha** currently contains the desktop shell, vault-state model, basic tray host, settings model, unlock dialog, UI theme, and a preview timer.

It intentionally does **not** claim to provide encrypted file storage yet.

## Planned components

### UI

Responsibilities:

- display state
- collect explicit user commands
- show security warnings
- expose settings
- never perform cryptography directly

### Vault controller

Responsibilities:

- own the locked/unlocked state machine
- coordinate unlock and lock sequences
- enforce policy
- coordinate filesystem mount/unmount
- coordinate Windows lifecycle events

### Key-management service

Responsibilities:

- create the random Vault Master Key
- derive password keys
- wrap/unwrap the Vault Master Key
- manage recovery-key slots
- minimize secret lifetime in memory

UI code should never retain the master key.

### Storage engine

Responsibilities:

- parse/write the versioned vault header
- authenticated encrypted metadata
- encrypted file blocks
- crash-safe commits
- integrity validation
- migrations between supported format versions

### Filesystem adapter

Responsibilities:

- expose the unlocked vault to normal Windows file operations
- map Explorer operations to the encrypted storage engine
- track open handles
- flush writes before unmounting
- support safe read-only recovery when required

### Windows integration

Responsibilities:

- notification-area icon
- Windows lock/unlock session notifications
- sleep/hibernate notifications
- sign-out/shutdown handling where possible
- startup integration
- optional hotkeys

## State machine

High-level states:

```text
Locked
  |
  | valid credential
  v
Unlocking
  |
  | key unwrap + metadata validation + mount
  v
Unlocked
  |
  | manual/policy/system event
  v
Locking
  |
  | stop writes + flush + unmount + clear key material
  v
Locked
```

Failure during **Unlocking** returns to **Locked**.

Failure during **Locking** must be surfaced clearly; the UI must never display "Locked" while a usable mount remains active.

## Dependency rule

Security-sensitive implementation should use established platform or reviewed library primitives. The project should avoid unnecessary dependencies and should document the reason for every security-critical package.

## Logging boundary

Logs may contain:

- application version
- non-sensitive error category
- state transition type
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

The application version and vault-format version are separate. A future app update may remain compatible with older vault formats. Vault-format migration must be explicit, tested, and recoverable.
