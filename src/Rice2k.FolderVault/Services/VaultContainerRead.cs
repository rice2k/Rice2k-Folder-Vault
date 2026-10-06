using System;
using System.IO;
using System.Linq;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed partial class VaultContainerService
{
    public int ReadFileRangeByEntryId(
        string vaultPath,
        VaultSessionKey sessionKey,
        string entryId,
        long offset,
        Span<byte> destination)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("An entry identifier is required.", nameof(entryId));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        using var input = new FileStream(
            Path.GetFullPath(vaultPath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var header = ReadHeader(input, out var payloadOffset);
        input.Position = payloadOffset;

        var metadata = _metadata.ReadMetadata(input, header, sessionKey);
        var recordsOffset = input.Position;

        var entry = metadata.Entries.FirstOrDefault(e =>
            string.Equals(e.EntryId, entryId, StringComparison.Ordinal) &&
            string.Equals(e.EntryType, "file", StringComparison.Ordinal))
            ?? throw new FileNotFoundException("The requested file was not found in the vault.");

        if (destination.Length == 0 || offset >= entry.PlaintextLength)
            return 0;

        input.Position = recordsOffset;

        var contentService = new VaultContentService();

        if (!contentService.TryReadContentRange(
                input,
                header,
                sessionKey,
                entry.ContentRecordId,
                offset,
                destination,
                out var bytesRead,
                out var recordInfo) ||
            recordInfo is null)
        {
            throw new InvalidDataException("The encrypted content record referenced by metadata was not found.");
        }

        if (recordInfo.PlaintextLength != entry.PlaintextLength)
            throw new InvalidDataException("Encrypted content length does not match vault metadata.");

        return bytesRead;
    }
}
