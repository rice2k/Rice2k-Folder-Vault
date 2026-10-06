using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed partial class VaultContainerService
{
    public IReadOnlyList<VaultEntryMetadata> ListDirectory(
        string vaultPath,
        VaultSessionKey sessionKey,
        string? directoryId = null)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        var metadata = ReadMetadata(vaultPath, sessionKey);
        var targetDirectoryId = string.IsNullOrWhiteSpace(directoryId)
            ? metadata.RootDirectoryId
            : directoryId;

        EnsureDirectoryExists(metadata, targetDirectoryId);

        return metadata.Entries
            .Where(e => string.Equals(e.ParentDirectoryId, targetDirectoryId, StringComparison.Ordinal))
            .OrderByDescending(e => string.Equals(e.EntryType, "directory", StringComparison.Ordinal))
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public VaultEntryMetadata CreateDirectory(
        string vaultPath,
        VaultSessionKey sessionKey,
        string parentDirectoryId,
        string directoryName)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        var normalizedName = directoryName?.Trim() ?? string.Empty;
        ValidateRootEntryName(normalizedName);

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".mkdir-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            VaultEntryMetadata created;

            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

                EnsureDirectoryExists(metadata, parentDirectoryId);
                EnsureNameAvailable(metadata, parentDirectoryId, normalizedName);

                created = new VaultEntryMetadata
                {
                    EntryId = Guid.NewGuid().ToString("N"),
                    ParentDirectoryId = parentDirectoryId,
                    Name = normalizedName,
                    EntryType = "directory",
                    PlaintextLength = 0,
                    LastWriteTimeUtcTicks = DateTime.UtcNow.Ticks,
                    ContentRecordId = string.Empty
                };

                metadata.Entries.Add(created);
                metadata.Revision++;

                RewriteMetadataAndCopyAllRecords(
                    input,
                    recordsOffset,
                    rewriteTemp,
                    header,
                    sessionKey,
                    metadata);
            }

            File.Move(rewriteTemp, fullVaultPath, true);
            return created;
        }
        finally
        {
            TryDelete(rewriteTemp);
        }
    }

    public VaultEntryMetadata RenameEntry(
        string vaultPath,
        VaultSessionKey sessionKey,
        string entryId,
        string newName)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("An entry identifier is required.", nameof(entryId));

        var normalizedName = newName?.Trim() ?? string.Empty;
        ValidateRootEntryName(normalizedName);

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".rename-entry-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            VaultEntryMetadata entry;

            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

                entry = metadata.Entries.FirstOrDefault(e =>
                    string.Equals(e.EntryId, entryId, StringComparison.Ordinal))
                    ?? throw new FileNotFoundException("The requested vault item was not found.");

                EnsureNameAvailable(metadata, entry.ParentDirectoryId, normalizedName, entry.EntryId);

                if (string.Equals(entry.Name, normalizedName, StringComparison.Ordinal))
                    return entry;

                entry.Name = normalizedName;
                entry.LastWriteTimeUtcTicks = DateTime.UtcNow.Ticks;
                metadata.Revision++;

                RewriteMetadataAndCopyAllRecords(
                    input,
                    recordsOffset,
                    rewriteTemp,
                    header,
                    sessionKey,
                    metadata);
            }

            File.Move(rewriteTemp, fullVaultPath, true);
            return entry;
        }
        finally
        {
            TryDelete(rewriteTemp);
        }
    }

    public void DeleteEntry(
        string vaultPath,
        VaultSessionKey sessionKey,
        string entryId,
        bool recursive = false)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("An entry identifier is required.", nameof(entryId));

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".delete-entry-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

                var entry = metadata.Entries.FirstOrDefault(e =>
                    string.Equals(e.EntryId, entryId, StringComparison.Ordinal))
                    ?? throw new FileNotFoundException("The requested vault item was not found.");

                var idsToDelete = new HashSet<string>(StringComparer.Ordinal) { entry.EntryId };

                if (string.Equals(entry.EntryType, "directory", StringComparison.Ordinal))
                {
                    var directChildrenExist = metadata.Entries.Any(e =>
                        string.Equals(e.ParentDirectoryId, entry.EntryId, StringComparison.Ordinal));

                    if (directChildrenExist && !recursive)
                        throw new IOException("The folder is not empty. Recursive deletion was not requested.");

                    if (recursive)
                        CollectDescendantIds(metadata, entry.EntryId, idsToDelete);
                }

                metadata.Entries.RemoveAll(e => idsToDelete.Contains(e.EntryId));
                metadata.Revision++;

                var retainedRecordIds = new HashSet<string>(
                    metadata.Entries
                        .Where(e =>
                            string.Equals(e.EntryType, "file", StringComparison.Ordinal) &&
                            !string.IsNullOrWhiteSpace(e.ContentRecordId))
                        .Select(e => e.ContentRecordId),
                    StringComparer.Ordinal);

                RewriteWithRetainedRecords(
                    input,
                    recordsOffset,
                    rewriteTemp,
                    header,
                    sessionKey,
                    metadata,
                    retainedRecordIds);
            }

            File.Move(rewriteTemp, fullVaultPath, true);
        }
        finally
        {
            TryDelete(rewriteTemp);
        }
    }

    public VaultEntryMetadata ImportFileToDirectory(
        string vaultPath,
        VaultSessionKey sessionKey,
        string sourceFilePath,
        string parentDirectoryId,
        string? vaultEntryName = null)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("A source file is required.", nameof(sourceFilePath));

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var fullSourcePath = Path.GetFullPath(sourceFilePath);

        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("The source file could not be found.", fullSourcePath);

        if (string.Equals(fullVaultPath, fullSourcePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The vault container cannot be imported into itself.");

        var entryName = string.IsNullOrWhiteSpace(vaultEntryName)
            ? Path.GetFileName(fullSourcePath)
            : vaultEntryName.Trim();

        ValidateRootEntryName(entryName);

        var encryptedRecordTemp = fullVaultPath + ".record-" + Guid.NewGuid().ToString("N") + ".tmp";
        var rewriteTemp = fullVaultPath + ".import-dir-" + Guid.NewGuid().ToString("N") + ".tmp";

        var initialHeader = ReadHeader(fullVaultPath);
        VaultContentRecordInfo recordInfo;

        try
        {
            using (var source = new FileStream(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var encryptedRecord = new FileStream(encryptedRecordTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var contentService = new VaultContentService();
                recordInfo = contentService.WriteEncryptedContent(
                    encryptedRecord,
                    initialHeader,
                    sessionKey,
                    source,
                    source.Length);
                encryptedRecord.Flush(true);
            }

            VaultEntryMetadata entry;

            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var currentHeader = ReadHeader(input, out var payloadOffset);

                if (!string.Equals(
                        currentHeader.VaultIdBase64,
                        initialHeader.VaultIdBase64,
                        StringComparison.Ordinal))
                {
                    throw new IOException("The vault changed while the import was being prepared.");
                }

                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, currentHeader, sessionKey);
                var existingRecordsOffset = input.Position;

                EnsureDirectoryExists(metadata, parentDirectoryId);
                EnsureNameAvailable(metadata, parentDirectoryId, entryName);

                entry = new VaultEntryMetadata
                {
                    EntryId = Guid.NewGuid().ToString("N"),
                    ParentDirectoryId = parentDirectoryId,
                    Name = entryName,
                    EntryType = "file",
                    PlaintextLength = recordInfo.PlaintextLength,
                    LastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(fullSourcePath).Ticks,
                    ContentRecordId = recordInfo.RecordIdBase64
                };

                metadata.Entries.Add(entry);
                metadata.Revision++;

                var headerBytes = SerializeHeader(currentHeader);

                using var output = new FileStream(rewriteTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(Magic, 0, Magic.Length);
                WriteHeaderLength(output, headerBytes.Length);
                output.Write(headerBytes, 0, headerBytes.Length);
                _metadata.WriteMetadata(output, currentHeader, sessionKey, metadata);

                input.Position = existingRecordsOffset;
                input.CopyTo(output);

                using var encryptedRecord = new FileStream(
                    encryptedRecordTemp,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                encryptedRecord.CopyTo(output);
                output.Flush(true);
            }

            File.Move(rewriteTemp, fullVaultPath, true);
            return entry;
        }
        finally
        {
            TryDelete(encryptedRecordTemp);
            TryDelete(rewriteTemp);
        }
    }

    public VaultEntryMetadata ExportFileByEntryId(
        string vaultPath,
        VaultSessionKey sessionKey,
        string entryId,
        string destinationFilePath,
        bool overwrite = false)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("An entry identifier is required.", nameof(entryId));
        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("A destination file is required.", nameof(destinationFilePath));

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var fullDestinationPath = Path.GetFullPath(destinationFilePath);

        if (string.Equals(fullVaultPath, fullDestinationPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The vault container cannot be overwritten by an exported file.");

        if (File.Exists(fullDestinationPath) && !overwrite)
            throw new IOException("The destination file already exists.");

        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var partialPath = fullDestinationPath + ".partial-" + Guid.NewGuid().ToString("N");

        try
        {
            VaultEntryMetadata entry;

            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var contentRecordsOffset = input.Position;

                entry = metadata.Entries.FirstOrDefault(e =>
                    string.Equals(e.EntryId, entryId, StringComparison.Ordinal) &&
                    string.Equals(e.EntryType, "file", StringComparison.Ordinal))
                    ?? throw new FileNotFoundException("The requested file was not found in the vault.");

                input.Position = contentRecordsOffset;

                using var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var contentService = new VaultContentService();

                if (!contentService.TryFindAndDecryptContent(
                        input,
                        header,
                        sessionKey,
                        output,
                        entry.ContentRecordId,
                        out var recordInfo) ||
                    recordInfo is null)
                {
                    throw new InvalidDataException("The encrypted content record referenced by metadata was not found.");
                }

                if (recordInfo.PlaintextLength != entry.PlaintextLength)
                    throw new InvalidDataException("Encrypted content length does not match vault metadata.");

                output.Flush(true);
            }

            File.Move(partialPath, fullDestinationPath, overwrite);
            RestoreTimestampBestEffort(fullDestinationPath, entry.LastWriteTimeUtcTicks);
            return entry;
        }
        finally
        {
            TryDelete(partialPath);
        }
    }

    public string GetDirectoryPath(
        VaultMetadata metadata,
        string directoryId)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        if (string.Equals(directoryId, metadata.RootDirectoryId, StringComparison.Ordinal))
            return "\";

        var names = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = directoryId;

        while (!string.Equals(currentId, metadata.RootDirectoryId, StringComparison.Ordinal))
        {
            if (!visited.Add(currentId))
                throw new InvalidDataException("A directory cycle exists in vault metadata.");

            var directory = metadata.Entries.FirstOrDefault(e =>
                string.Equals(e.EntryId, currentId, StringComparison.Ordinal) &&
                string.Equals(e.EntryType, "directory", StringComparison.Ordinal))
                ?? throw new InvalidDataException("A directory referenced by vault metadata is missing.");

            names.Push(directory.Name);
            currentId = directory.ParentDirectoryId;
        }

        return "\" + string.Join("\", names);
    }

    private void RewriteMetadataAndCopyAllRecords(
        FileStream input,
        long recordsOffset,
        string rewriteTemp,
        VaultHeader header,
        VaultSessionKey sessionKey,
        VaultMetadata metadata)
    {
        var headerBytes = SerializeHeader(header);

        using var output = new FileStream(rewriteTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(Magic, 0, Magic.Length);
        WriteHeaderLength(output, headerBytes.Length);
        output.Write(headerBytes, 0, headerBytes.Length);
        _metadata.WriteMetadata(output, header, sessionKey, metadata);

        input.Position = recordsOffset;
        input.CopyTo(output);
        output.Flush(true);
    }

    private static void EnsureDirectoryExists(VaultMetadata metadata, string directoryId)
    {
        if (string.Equals(directoryId, metadata.RootDirectoryId, StringComparison.Ordinal))
            return;

        var found = metadata.Entries.Any(e =>
            string.Equals(e.EntryId, directoryId, StringComparison.Ordinal) &&
            string.Equals(e.EntryType, "directory", StringComparison.Ordinal));

        if (!found)
            throw new DirectoryNotFoundException("The requested vault directory does not exist.");
    }

    private static void EnsureNameAvailable(
        VaultMetadata metadata,
        string parentDirectoryId,
        string name,
        string? excludedEntryId = null)
    {
        var duplicate = metadata.Entries.Any(e =>
            !string.Equals(e.EntryId, excludedEntryId, StringComparison.Ordinal) &&
            string.Equals(e.ParentDirectoryId, parentDirectoryId, StringComparison.Ordinal) &&
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
            throw new IOException("An item with the same name already exists in this vault folder.");
    }

    private static void CollectDescendantIds(
        VaultMetadata metadata,
        string directoryId,
        HashSet<string> ids)
    {
        foreach (var child in metadata.Entries
                     .Where(e => string.Equals(e.ParentDirectoryId, directoryId, StringComparison.Ordinal))
                     .ToList())
        {
            if (!ids.Add(child.EntryId))
                continue;

            if (string.Equals(child.EntryType, "directory", StringComparison.Ordinal))
                CollectDescendantIds(metadata, child.EntryId, ids);
        }
    }

    private static void RestoreTimestampBestEffort(string path, long utcTicks)
    {
        if (utcTicks <= 0)
            return;

        try
        {
            File.SetLastWriteTimeUtc(path, new DateTime(utcTicks, DateTimeKind.Utc));
        }
        catch
        {
            // Timestamp restoration is best-effort and must not invalidate exported content.
        }
    }
}
