using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultContentService
{
    private static readonly byte[] RecordMagic = Encoding.ASCII.GetBytes("FILE");
    private static readonly byte[] KeyInfoPrefix = Encoding.ASCII.GetBytes("Rice2kFolderVault/content-key/v1");
    private const int RecordVersion = 1;
    private const int RecordIdSize = 16;
    public const int DefaultChunkSize = 1024 * 1024;
    private const int MaximumChunkSize = 8 * 1024 * 1024;

    public VaultContentRecordInfo WriteEncryptedContent(
        Stream destination,
        VaultHeader header,
        VaultSessionKey sessionKey,
        Stream plaintextSource,
        long plaintextLength,
        int chunkSize = DefaultChunkSize)
    {
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (plaintextSource is null)
            throw new ArgumentNullException(nameof(plaintextSource));
        if (!destination.CanWrite)
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        if (!plaintextSource.CanRead)
            throw new ArgumentException("Source stream must be readable.", nameof(plaintextSource));
        if (plaintextLength < 0)
            throw new ArgumentOutOfRangeException(nameof(plaintextLength));
        if (chunkSize <= 0 || chunkSize > MaximumChunkSize)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        var chunkCountLong = plaintextLength == 0
            ? 0
            : (plaintextLength + chunkSize - 1) / chunkSize;

        if (chunkCountLong > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(plaintextLength), "File requires too many encrypted chunks.");

        var chunkCount = (int)chunkCountLong;
        var recordId = RandomNumberGenerator.GetBytes(RecordIdSize);
        var vaultId = Convert.FromBase64String(header.VaultIdBase64);
        var contentKey = DeriveContentKey(sessionKey, vaultId, recordId);

        destination.Write(RecordMagic, 0, RecordMagic.Length);
        WriteInt32(destination, RecordVersion);
        destination.Write(recordId, 0, recordId.Length);
        WriteInt32(destination, chunkSize);
        WriteInt64(destination, plaintextLength);
        WriteInt32(destination, chunkCount);

        var plaintextBuffer = new byte[chunkSize];

        try
        {
            long remaining = plaintextLength;

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var currentLength = (int)Math.Min(chunkSize, remaining);
                ReadExactly(plaintextSource, plaintextBuffer.AsSpan(0, currentLength));

                var ciphertext = new byte[currentLength];
                var nonce = RandomNumberGenerator.GetBytes(VaultCryptoService.AesGcmNonceSize);
                var tag = new byte[VaultCryptoService.AesGcmTagSize];
                var aad = BuildChunkAssociatedData(vaultId, recordId, chunkIndex, currentLength, plaintextLength);

                try
                {
                    using var aes = new AesGcm(contentKey, VaultCryptoService.AesGcmTagSize);
                    aes.Encrypt(
                        nonce,
                        plaintextBuffer.AsSpan(0, currentLength),
                        ciphertext,
                        tag,
                        aad);

                    WriteInt32(destination, chunkIndex);
                    WriteInt32(destination, currentLength);
                    destination.Write(nonce, 0, nonce.Length);
                    destination.Write(tag, 0, tag.Length);
                    destination.Write(ciphertext, 0, ciphertext.Length);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(ciphertext);
                    CryptographicOperations.ZeroMemory(nonce);
                    CryptographicOperations.ZeroMemory(tag);
                    CryptographicOperations.ZeroMemory(aad);
                    CryptographicOperations.ZeroMemory(plaintextBuffer.AsSpan(0, currentLength));
                }

                remaining -= currentLength;
            }

            if (remaining != 0)
                throw new EndOfStreamException("Plaintext stream ended before the declared file length.");

            return new VaultContentRecordInfo
            {
                RecordIdBase64 = Convert.ToBase64String(recordId),
                PlaintextLength = plaintextLength,
                ChunkSize = chunkSize,
                ChunkCount = chunkCount
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBuffer);
            CryptographicOperations.ZeroMemory(recordId);
            CryptographicOperations.ZeroMemory(vaultId);
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    public VaultContentRecordInfo ReadEncryptedContent(
        Stream source,
        VaultHeader header,
        VaultSessionKey sessionKey,
        Stream plaintextDestination,
        string? expectedRecordIdBase64 = null)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (plaintextDestination is null)
            throw new ArgumentNullException(nameof(plaintextDestination));
        if (!source.CanRead)
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        if (!plaintextDestination.CanWrite)
            throw new ArgumentException("Destination stream must be writable.", nameof(plaintextDestination));

        Span<byte> magic = stackalloc byte[4];
        ReadExactly(source, magic);
        if (!magic.SequenceEqual(RecordMagic))
            throw new InvalidDataException("Encrypted file record signature is invalid.");

        var version = ReadInt32(source);
        if (version != RecordVersion)
            throw new InvalidDataException($"Unsupported encrypted file-record version: {version}.");

        var recordId = new byte[RecordIdSize];
        ReadExactly(source, recordId);

        if (!string.IsNullOrEmpty(expectedRecordIdBase64))
        {
            var expected = Convert.FromBase64String(expectedRecordIdBase64);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(recordId, expected))
                    throw new InvalidDataException("Encrypted file record identifier does not match metadata.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
            }
        }

        var chunkSize = ReadInt32(source);
        var plaintextLength = ReadInt64(source);
        var chunkCount = ReadInt32(source);

        if (chunkSize <= 0 || chunkSize > MaximumChunkSize)
            throw new InvalidDataException("Encrypted file chunk size is invalid.");
        if (plaintextLength < 0)
            throw new InvalidDataException("Encrypted file plaintext length is invalid.");

        var expectedChunkCount = plaintextLength == 0
            ? 0L
            : (plaintextLength + chunkSize - 1) / chunkSize;

        if (chunkCount < 0 || chunkCount != expectedChunkCount)
            throw new InvalidDataException("Encrypted file chunk count is invalid.");

        var vaultId = Convert.FromBase64String(header.VaultIdBase64);
        var contentKey = DeriveContentKey(sessionKey, vaultId, recordId);
        long totalWritten = 0;

        try
        {
            for (var expectedIndex = 0; expectedIndex < chunkCount; expectedIndex++)
            {
                var chunkIndex = ReadInt32(source);
                var currentLength = ReadInt32(source);

                if (chunkIndex != expectedIndex)
                    throw new InvalidDataException("Encrypted file chunks are out of order.");
                if (currentLength <= 0 || currentLength > chunkSize)
                    throw new InvalidDataException("Encrypted file chunk length is invalid.");
                if (totalWritten + currentLength > plaintextLength)
                    throw new InvalidDataException("Encrypted file exceeds declared plaintext length.");

                var nonce = new byte[VaultCryptoService.AesGcmNonceSize];
                var tag = new byte[VaultCryptoService.AesGcmTagSize];
                var ciphertext = new byte[currentLength];
                var plaintext = new byte[currentLength];

                ReadExactly(source, nonce);
                ReadExactly(source, tag);
                ReadExactly(source, ciphertext);

                var aad = BuildChunkAssociatedData(
                    vaultId,
                    recordId,
                    chunkIndex,
                    currentLength,
                    plaintextLength);

                try
                {
                    using var aes = new AesGcm(contentKey, VaultCryptoService.AesGcmTagSize);
                    aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                    plaintextDestination.Write(plaintext, 0, plaintext.Length);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(nonce);
                    CryptographicOperations.ZeroMemory(tag);
                    CryptographicOperations.ZeroMemory(ciphertext);
                    CryptographicOperations.ZeroMemory(plaintext);
                    CryptographicOperations.ZeroMemory(aad);
                }

                totalWritten += currentLength;
            }

            if (totalWritten != plaintextLength)
                throw new InvalidDataException("Encrypted file length does not match the record header.");

            return new VaultContentRecordInfo
            {
                RecordIdBase64 = Convert.ToBase64String(recordId),
                PlaintextLength = plaintextLength,
                ChunkSize = chunkSize,
                ChunkCount = chunkCount
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recordId);
            CryptographicOperations.ZeroMemory(vaultId);
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    public VaultContentRecordInfo InspectAndSkipEncryptedContent(Stream source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (!source.CanRead)
            throw new ArgumentException("Source stream must be readable.", nameof(source));

        Span<byte> magic = stackalloc byte[4];
        ReadExactly(source, magic);
        if (!magic.SequenceEqual(RecordMagic))
            throw new InvalidDataException("Encrypted file record signature is invalid.");

        var version = ReadInt32(source);
        if (version != RecordVersion)
            throw new InvalidDataException($"Unsupported encrypted file-record version: {version}.");

        var recordId = new byte[RecordIdSize];
        ReadExactly(source, recordId);

        try
        {
            var chunkSize = ReadInt32(source);
            var plaintextLength = ReadInt64(source);
            var chunkCount = ReadInt32(source);

            if (chunkSize <= 0 || chunkSize > MaximumChunkSize)
                throw new InvalidDataException("Encrypted file chunk size is invalid.");
            if (plaintextLength < 0)
                throw new InvalidDataException("Encrypted file plaintext length is invalid.");

            var expectedChunkCount = plaintextLength == 0
                ? 0L
                : (plaintextLength + chunkSize - 1) / chunkSize;

            if (chunkCount < 0 || chunkCount != expectedChunkCount)
                throw new InvalidDataException("Encrypted file chunk count is invalid.");

            long totalLength = 0;

            for (var expectedIndex = 0; expectedIndex < chunkCount; expectedIndex++)
            {
                var chunkIndex = ReadInt32(source);
                var currentLength = ReadInt32(source);

                if (chunkIndex != expectedIndex)
                    throw new InvalidDataException("Encrypted file chunks are out of order.");
                if (currentLength <= 0 || currentLength > chunkSize)
                    throw new InvalidDataException("Encrypted file chunk length is invalid.");
                if (totalLength + currentLength > plaintextLength)
                    throw new InvalidDataException("Encrypted file exceeds declared plaintext length.");

                SkipExactly(
                    source,
                    VaultCryptoService.AesGcmNonceSize +
                    VaultCryptoService.AesGcmTagSize +
                    currentLength);

                totalLength += currentLength;
            }

            if (totalLength != plaintextLength)
                throw new InvalidDataException("Encrypted file length does not match the record header.");

            return new VaultContentRecordInfo
            {
                RecordIdBase64 = Convert.ToBase64String(recordId),
                PlaintextLength = plaintextLength,
                ChunkSize = chunkSize,
                ChunkCount = chunkCount
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recordId);
        }
    }

    public bool TryFindAndDecryptContent(
        Stream source,
        VaultHeader header,
        VaultSessionKey sessionKey,
        Stream plaintextDestination,
        string expectedRecordIdBase64,
        out VaultContentRecordInfo? recordInfo)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (!source.CanSeek)
            throw new ArgumentException("Content-record search requires a seekable source stream.", nameof(source));
        if (string.IsNullOrWhiteSpace(expectedRecordIdBase64))
            throw new ArgumentException("A content-record identifier is required.", nameof(expectedRecordIdBase64));

        recordInfo = null;

        while (source.Position < source.Length)
        {
            var recordStart = source.Position;
            var inspected = InspectAndSkipEncryptedContent(source);

            if (!string.Equals(inspected.RecordIdBase64, expectedRecordIdBase64, StringComparison.Ordinal))
                continue;

            source.Position = recordStart;
            recordInfo = ReadEncryptedContent(
                source,
                header,
                sessionKey,
                plaintextDestination,
                expectedRecordIdBase64);
            return true;
        }

        return false;
    }

    private static void SkipExactly(Stream source, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (source.CanSeek)
        {
            if (source.Length - source.Position < count)
                throw new EndOfStreamException("Encrypted file record ended unexpectedly.");

            source.Seek(count, SeekOrigin.Current);
            return;
        }

        var buffer = new byte[Math.Min(8192, Math.Max(1, count))];
        try
        {
            var remaining = count;
            while (remaining > 0)
            {
                var toRead = Math.Min(buffer.Length, remaining);
                var read = source.Read(buffer, 0, toRead);
                if (read == 0)
                    throw new EndOfStreamException("Encrypted file record ended unexpectedly.");
                remaining -= read;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static byte[] DeriveContentKey(VaultSessionKey sessionKey, byte[] vaultId, byte[] recordId)
    {
        var info = new byte[KeyInfoPrefix.Length + recordId.Length];
        Buffer.BlockCopy(KeyInfoPrefix, 0, info, 0, KeyInfoPrefix.Length);
        Buffer.BlockCopy(recordId, 0, info, KeyInfoPrefix.Length, recordId.Length);

        try
        {
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                sessionKey.DangerousGetKey(),
                32,
                vaultId,
                info);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(info);
        }
    }

    private static byte[] BuildChunkAssociatedData(
        byte[] vaultId,
        byte[] recordId,
        int chunkIndex,
        int chunkLength,
        long plaintextLength)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("R2FV-FILE"), 0, 9);
        WriteInt32(stream, RecordVersion);
        WriteInt32(stream, vaultId.Length);
        stream.Write(vaultId, 0, vaultId.Length);
        WriteInt32(stream, recordId.Length);
        stream.Write(recordId, 0, recordId.Length);
        WriteInt32(stream, chunkIndex);
        WriteInt32(stream, chunkLength);
        WriteInt64(stream, plaintextLength);
        return stream.ToArray();
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static int ReadInt32(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(stream, buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static long ReadInt64(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        ReadExactly(stream, buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
                throw new EndOfStreamException("Encrypted file record ended unexpectedly.");
            total += read;
        }
    }
}
