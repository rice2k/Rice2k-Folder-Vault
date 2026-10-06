using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed partial class VaultContainerService
{
    public VaultEntryMetadata RenameRootFile(
        string vaultPath,
        VaultSessionKey sessionKey,
        string currentName,
        string newName)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(currentName))
            throw new ArgumentException("The current vault entry name is required.", nameof(currentName));

        var normalizedNewName = newName?.Trim() ?? string.Empty;
        ValidateRootEntryName(normalizedNewName);

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".rename-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            VaultEntryMetadata renamed;

            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;

                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

                renamed = metadata.Entries.FirstOrDefault(e =>
                    string.Equals(e.ParentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal) &&
                    string.Equals(e.EntryType, "file", StringComparison.Ordinal) &&
                    string.Equals(e.Name, currentName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileNotFoundException("The requested file was not found in the vault.");

                var duplicateExists = metadata.Entries.Any(e =>
                    !ReferenceEquals(e, renamed) &&
                    string.Equals(e.ParentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal) &&
                    string.Equals(e.Name, normalizedNewName, StringComparison.OrdinalIgnoreCase));

                if (duplicateExists)
                    throw new IOException("An item with the same name already exists in the vault root.");

                if (string.Equals(renamed.Name, normalizedNewName, StringComparison.Ordinal))
                    return renamed;

                renamed.Name = normalizedNewName;
                metadata.Revision++;

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

            File.Move(rewriteTemp, fullVaultPath, true);
            return renamed;
        }
        finally
        {
            TryDelete(rewriteTemp);
        }
    }

    public void DeleteRootFile(
        string vaultPath,
        VaultSessionKey sessionKey,
        string entryName)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("A vault entry name is required.", nameof(entryName));

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".delete-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;

                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

                var entry = metadata.Entries.FirstOrDefault(e =>
                    string.Equals(e.ParentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal) &&
                    string.Equals(e.EntryType, "file", StringComparison.Ordinal) &&
                    string.Equals(e.Name, entryName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileNotFoundException("The requested file was not found in the vault.");

                metadata.Entries.Remove(entry);
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

    public void CompactVault(
        string vaultPath,
        VaultSessionKey sessionKey)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        var fullVaultPath = Path.GetFullPath(vaultPath);
        var rewriteTemp = fullVaultPath + ".compact-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var input = new FileStream(fullVaultPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var header = ReadHeader(input, out var payloadOffset);
                input.Position = payloadOffset;

                var metadata = _metadata.ReadMetadata(input, header, sessionKey);
                var recordsOffset = input.Position;

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

    private void RewriteWithRetainedRecords(
        FileStream input,
        long recordsOffset,
        string rewriteTemp,
        VaultHeader header,
        VaultSessionKey sessionKey,
        VaultMetadata metadata,
        HashSet<string> retainedRecordIds)
    {
        var headerBytes = SerializeHeader(header);
        var contentService = new VaultContentService();

        using var output = new FileStream(rewriteTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(Magic, 0, Magic.Length);
        WriteHeaderLength(output, headerBytes.Length);
        output.Write(headerBytes, 0, headerBytes.Length);
        _metadata.WriteMetadata(output, header, sessionKey, metadata);

        input.Position = recordsOffset;

        while (input.Position < input.Length)
        {
            var recordStart = input.Position;
            var recordInfo = contentService.InspectAndSkipEncryptedContent(input);
            var recordEnd = input.Position;

            if (!retainedRecordIds.Contains(recordInfo.RecordIdBase64))
                continue;

            input.Position = recordStart;
            CopyExactly(input, output, recordEnd - recordStart);
            input.Position = recordEnd;
        }

        output.Flush(true);
    }

    private static void CopyExactly(Stream source, Stream destination, long count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var buffer = new byte[1024 * 1024];

        try
        {
            long remaining = count;

            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = source.Read(buffer, 0, toRead);

                if (read == 0)
                    throw new EndOfStreamException("The vault container ended while copying an encrypted record.");

                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }
        finally
        {
            Array.Clear(buffer, 0, buffer.Length);
        }
    }
}
