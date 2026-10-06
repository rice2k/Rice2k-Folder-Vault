# Windows Filesystem Integration Plan

## Decision

Rice2k Folder Vault will use **Dokan/DokanNet** for the first Windows Explorer filesystem implementation.

Selected binding:

- NuGet: `DokanNet 2.3.0.3`
- Project target: .NET 8 / Windows
- Binding license: MIT
- Required Windows runtime: Dokany 2.x driver/library

The current DokanNet package explicitly targets .NET 8. The Dokany runtime provides the signed Windows driver and user-mode library required to expose a user-mode filesystem as a normal Windows drive.

## Why DokanNet

The project needs:

- a normal Windows drive letter such as `V:\`
- Explorer and standard Win32 file API compatibility
- C#/.NET 8 callbacks
- read/write/create/rename/delete callbacks
- open-handle lifecycle notifications
- mount/unmount control from the tray application
- no custom Rice2k kernel driver

DokanNet supplies those callbacks through `IDokanOperations` / `IDokanOperations2` and mounts through `DokanInstanceBuilder`.

## Alternative reviewed

WinFsp remains a technically viable alternative and has a maintained .NET package. It was not selected for the first adapter because DokanNet exposes a direct .NET 8 binding and callback model that fits the current C# application with less custom interop.

The filesystem adapter is isolated behind Rice2k Folder Vault services so the storage format is not tied to Dokan. A later migration to another mount provider must not require re-encrypting `.rvault` files.

## Phased implementation

### Phase A — Read-only Explorer proof — source implementation complete

- [x] mount `V:\` through DokanNet
- [x] expose root and nested encrypted directories
- [x] enumerate encrypted metadata as normal filenames
- [x] report sizes/timestamps
- [x] support offset-based authenticated reads directly from encrypted FILE chunks
- [x] deny create/write/delete/rename
- [x] request unmount before VMK destruction on lock
- [ ] verify a full mount/read/unmount cycle on a Windows machine with the Dokany 2.x runtime installed

This phase validates Windows filesystem semantics without risking encrypted write corruption.

### Phase B — Open-handle model

- track per-handle state
- enforce locked/unlocked state
- track active readers
- coordinate lock requests with open handles
- support cache/flush semantics

### Phase C — Writable filesystem

- create files
- random-access writes
- truncate/extend
- rename
- delete
- directories
- durable metadata commits
- rollback after failed writes

Writable mounting will not be enabled until the storage engine has a transaction strategy that can survive interrupted writes.

## Storage changes required

The current `FILE` format is chunked but was initially written for sequential import/export.

Explorer requires:

- offset-based reads
- efficient record lookup
- eventually offset-based writes
- file-handle concurrency
- durable flush semantics

The storage engine now exposes authenticated **range reads** from encrypted FILE chunks without creating plaintext temporary files. The current implementation scans encrypted FILE records by content-record ID for each read; record-offset indexing is a future performance optimization.

## Security boundary

Dokan is an access layer, not the encryption layer.

Dokan receives plaintext only while the vault is unlocked. The persistent `.rvault` file remains encrypted independently of Dokan.

Lock is complete only after:

1. new filesystem operations are blocked
2. active writes are committed or safely rejected
3. the virtual drive is unmounted
4. filesystem handles are released
5. the VMK session is destroyed

## Runtime prerequisite

DokanNet requires the Dokany Windows runtime/driver. The final installer should detect/install the supported runtime and report a clear prerequisite error instead of silently failing to mount.

## Alpha rule

Until writable transactions are designed and tested, the Dokan adapter should default to **read-only** even though the existing Vault Contents window can already import/export files explicitly.
