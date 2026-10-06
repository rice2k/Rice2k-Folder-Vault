using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultMetadataService
{
    private static readonly byte[] SegmentMagic = Encoding.ASCII.GetBytes("META");
    private static readonly byte[] KeyInfo = Encoding.ASCII.GetBytes("Rice2kFolderVault/metadata-key/v1");
    private const int SegmentVersion = 1;
    private const int MaximumMetadataCiphertextSize = 64 * 1024 * 1024;

    public void WriteInitialMetadata(Stream stream, VaultHeader header, VaultSessionKey sessionKey)
    {
        var metadata = new VaultMetadata();
        WriteMetadata(stream, header, sessionKey, metadata);
    }

    public void WriteMetadata(Stream stream, VaultHeader header, VaultSessionKey sessionKey, VaultMetadata metadata)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));
        if (metadata.MetadataVersion != 1)
            throw new InvalidDataException("Unsupported metadata version.");

        var vaultId = Convert.FromBase64String(header.VaultIdBase64);
        var metadataKey = DeriveMetadataKey(sessionKey, vaultId);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(metadata);
        var ciphertext = new byte[plaintext.Length];
        var nonce = RandomNumberGenerator.GetBytes(VaultCryptoService.AesGcmNonceSize);
        var tag = new byte[VaultCryptoService.AesGcmTagSize];
        var aad = BuildAssociatedData(vaultId);

        try
        {
            using var aes = new AesGcm(metadataKey, VaultCryptoService.AesGcmTagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            stream.Write(SegmentMagic, 0, SegmentMagic.Length);
            WriteInt32(stream, SegmentVersion);
            WriteInt32(stream, ciphertext.Length);
            stream.Write(nonce, 0, nonce.Length);
            stream.Write(tag, 0, tag.Length);
            stream.Write(ciphertext, 0, ciphertext.Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultId);
            CryptographicOperations.ZeroMemory(metadataKey);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    public VaultMetadata ReadMetadata(Stream stream, VaultHeader header, VaultSessionKey sessionKey)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        Span<byte> magic = stackalloc byte[4];
        ReadExactly(stream, magic);

        if (!magic.SequenceEqual(SegmentMagic))
            throw new InvalidDataException("Vault metadata segment is missing or invalid.");

        var segmentVersion = ReadInt32(stream);
        if (segmentVersion != SegmentVersion)
            throw new InvalidDataException($"Unsupported metadata segment version: {segmentVersion}.");

        var ciphertextLength = ReadInt32(stream);
        if (ciphertextLength <= 0 || ciphertextLength > MaximumMetadataCiphertextSize)
            throw new InvalidDataException("Vault metadata length is invalid.");

        var nonce = new byte[VaultCryptoService.AesGcmNonceSize];
        var tag = new byte[VaultCryptoService.AesGcmTagSize];
        var ciphertext = new byte[ciphertextLength];
        ReadExactly(stream, nonce);
        ReadExactly(stream, tag);
        ReadExactly(stream, ciphertext);

        var vaultId = Convert.FromBase64String(header.VaultIdBase64);
        var metadataKey = DeriveMetadataKey(sessionKey, vaultId);
        var plaintext = new byte[ciphertextLength];
        var aad = BuildAssociatedData(vaultId);

        try
        {
            using var aes = new AesGcm(metadataKey, VaultCryptoService.AesGcmTagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);

            var metadata = JsonSerializer.Deserialize<VaultMetadata>(plaintext)
                ?? throw new InvalidDataException("Vault metadata could not be decoded.");

            if (metadata.MetadataVersion != 1)
                throw new InvalidDataException("Unsupported decrypted metadata version.");

            return metadata;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(vaultId);
            CryptographicOperations.ZeroMemory(metadataKey);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static byte[] DeriveMetadataKey(VaultSessionKey sessionKey, byte[] vaultId)
    {
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            sessionKey.DangerousGetKey(),
            32,
            vaultId,
            KeyInfo);
    }

    private static byte[] BuildAssociatedData(byte[] vaultId)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("R2FV-META"), 0, 9);
        WriteInt32(stream, SegmentVersion);
        WriteInt32(stream, vaultId.Length);
        stream.Write(vaultId, 0, vaultId.Length);
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

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
                throw new EndOfStreamException("The vault metadata segment ended unexpectedly.");
            total += read;
        }
    }
}
