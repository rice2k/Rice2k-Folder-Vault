using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultContainerService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("R2FVLT01");
    private const int HeaderLengthSize = 4;
    private const int MaximumHeaderSize = 1024 * 1024;

    private readonly VaultCryptoService _crypto;
    private readonly VaultMetadataService _metadata = new();

    public VaultContainerService(VaultCryptoService crypto)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    public void CreateVault(string path, string password)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A vault path is required.", nameof(path));

        if (File.Exists(path))
            throw new IOException("A file already exists at the selected vault path.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var header = _crypto.CreateHeader(password);
        var headerBytes = SerializeHeader(header);

        if (!_crypto.TryUnwrapMasterKey(header, password, out var sessionKey) || sessionKey is null)
            throw new CryptographicException("The newly-created vault master key could not be reopened.");

        using (sessionKey)
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(Magic, 0, Magic.Length);
            WriteHeaderLength(stream, headerBytes.Length);
            stream.Write(headerBytes, 0, headerBytes.Length);
            _metadata.WriteInitialMetadata(stream, header, sessionKey);
            stream.Flush(true);
        }
    }

    public VaultHeader ReadHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadHeader(stream, out _);
    }

    public bool TryUnlock(string path, string password, out VaultSessionKey? sessionKey)
    {
        sessionKey = null;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = ReadHeader(stream, out var payloadOffset);

        if (!_crypto.TryUnwrapMasterKey(header, password, out sessionKey) || sessionKey is null)
            return false;

        try
        {
            stream.Position = payloadOffset;
            _metadata.ReadMetadata(stream, header, sessionKey);
            return true;
        }
        catch
        {
            sessionKey.Dispose();
            sessionKey = null;
            throw;
        }
    }

    public VaultMetadata ReadMetadata(string path, VaultSessionKey sessionKey)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = ReadHeader(stream, out var payloadOffset);
        stream.Position = payloadOffset;
        return _metadata.ReadMetadata(stream, header, sessionKey);
    }

    public VaultEntryMetadata ImportFile(
        string vaultPath,
        VaultSessionKey sessionKey,
        string sourceFilePath,
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
        var rewriteTemp = fullVaultPath + ".rewrite-" + Guid.NewGuid().ToString("N") + ".tmp";

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
                    throw new IOException("The vault changed while the import was being prepared.");

                input.Position = payloadOffset;
                var metadata = _metadata.ReadMetadata(input, currentHeader, sessionKey);
                var existingRecordsOffset = input.Position;

                if (metadata.Entries.Any(e =>
                    string.Equals(e.ParentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal) &&
                    string.Equals(e.Name, entryName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new IOException("An item with the same name already exists in the vault root.");
                }

                entry = new VaultEntryMetadata
                {
                    EntryId = Guid.NewGuid().ToString("N"),
                    ParentDirectoryId = metadata.RootDirectoryId,
                    Name = entryName,
                    EntryType = "file",
                    PlaintextLength = recordInfo.PlaintextLength,
                    LastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(fullSourcePath).Ticks,
                    ContentRecordId = recordInfo.RecordIdBase64
                };

                metadata.Entries.Add(entry);
                metadata.Revision++;

                var headerBytes = SerializeHeader(currentHeader);

                using (var output = new FileStream(rewriteTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
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

    public VaultEntryMetadata ExportFile(
        string vaultPath,
        VaultSessionKey sessionKey,
        string vaultEntryName,
        string destinationFilePath,
        bool overwrite = false)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrWhiteSpace(vaultEntryName))
            throw new ArgumentException("A vault entry name is required.", nameof(vaultEntryName));
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
                    string.Equals(e.ParentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal) &&
                    string.Equals(e.EntryType, "file", StringComparison.Ordinal) &&
                    string.Equals(e.Name, vaultEntryName, StringComparison.OrdinalIgnoreCase))
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

            if (entry.LastWriteTimeUtcTicks > 0)
            {
                try
                {
                    File.SetLastWriteTimeUtc(
                        fullDestinationPath,
                        new DateTime(entry.LastWriteTimeUtcTicks, DateTimeKind.Utc));
                }
                catch
                {
                    // Timestamp restoration is best-effort and must not invalidate exported content.
                }
            }

            return entry;
        }
        finally
        {
            TryDelete(partialPath);
        }
    }

    private static void ValidateRootEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) ||
            entryName is "." or ".." ||
            entryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            entryName.Contains(Path.DirectorySeparatorChar) ||
            entryName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The vault entry name is not a valid Windows file name.", nameof(entryName));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup is best-effort. Recovery/cleanup reporting will be added with diagnostics.
        }
    }

    public void ChangePassword(string path, string currentPassword, string newPassword)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = fullPath + ".rewrite";

        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var oldHeader = ReadHeader(input, out var payloadOffset);

        if (!_crypto.TryUnwrapMasterKey(oldHeader, currentPassword, out var validationSession) || validationSession is null)
            throw new UnauthorizedAccessException("The current vault password is incorrect.");

        using (validationSession)
        {
            input.Position = payloadOffset;
            _metadata.ReadMetadata(input, oldHeader, validationSession);
        }

        var newHeader = _crypto.RewrapMasterKey(oldHeader, currentPassword, newPassword);
        var newHeaderBytes = SerializeHeader(newHeader);

        try
        {
            input.Position = payloadOffset;

            using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                output.Write(Magic, 0, Magic.Length);
                WriteHeaderLength(output, newHeaderBytes.Length);
                output.Write(newHeaderBytes, 0, newHeaderBytes.Length);
                input.CopyTo(output);
                output.Flush(true);
            }

            input.Dispose();
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    internal static VaultHeader ReadHeader(Stream stream, out long payloadOffset)
    {
        Span<byte> magic = stackalloc byte[8];
        ReadExactly(stream, magic);

        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("This is not a Rice2k Folder Vault container.");

        Span<byte> lengthBytes = stackalloc byte[HeaderLengthSize];
        ReadExactly(stream, lengthBytes);
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);

        if (headerLength <= 0 || headerLength > MaximumHeaderSize)
            throw new InvalidDataException("Vault header length is invalid.");

        var headerBytes = new byte[headerLength];
        ReadExactly(stream, headerBytes);
        payloadOffset = stream.Position;

        var header = JsonSerializer.Deserialize<VaultHeader>(headerBytes)
            ?? throw new InvalidDataException("Vault header could not be decoded.");

        VaultCryptoService.ValidateHeader(header);
        return header;
    }

    private static byte[] SerializeHeader(VaultHeader header)
    {
        VaultCryptoService.ValidateHeader(header);
        return JsonSerializer.SerializeToUtf8Bytes(header, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static void WriteHeaderLength(Stream stream, int length)
    {
        Span<byte> lengthBytes = stackalloc byte[HeaderLengthSize];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, length);
        stream.Write(lengthBytes);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
                throw new EndOfStreamException("The vault container ended unexpectedly.");

            total += read;
        }
    }
}
