using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultCryptoService
{
    public const int MasterKeySize = 32;
    public const int SaltSize = 32;
    public const int VaultIdSize = 16;
    public const int AesGcmNonceSize = 12;
    public const int AesGcmTagSize = 16;

    public VaultHeader CreateHeader(string password)
    {
        ValidatePassword(password);

        var masterKey = RandomNumberGenerator.GetBytes(MasterKeySize);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var vaultId = RandomNumberGenerator.GetBytes(VaultIdSize);

        try
        {
            var header = new VaultHeader
            {
                VaultIdBase64 = Convert.ToBase64String(vaultId),
                KdfSaltBase64 = Convert.ToBase64String(salt)
            };

            WrapMasterKey(header, password, masterKey);
            return header;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(vaultId);
        }
    }

    public bool TryUnwrapMasterKey(VaultHeader header, string password, out VaultSessionKey? sessionKey)
    {
        sessionKey = null;

        if (string.IsNullOrEmpty(password))
            return false;

        ValidateHeader(header);

        var salt = Convert.FromBase64String(header.KdfSaltBase64);
        var nonce = Convert.FromBase64String(header.WrappedMasterKeyNonceBase64);
        var ciphertext = Convert.FromBase64String(header.WrappedMasterKeyCiphertextBase64);
        var tag = Convert.FromBase64String(header.WrappedMasterKeyTagBase64);
        var keyEncryptionKey = DeriveKeyEncryptionKey(password, salt, header);
        var masterKey = new byte[MasterKeySize];
        var aad = BuildAssociatedData(header);

        try
        {
            using var aes = new AesGcm(keyEncryptionKey, AesGcmTagSize);
            aes.Decrypt(nonce, ciphertext, tag, masterKey, aad);
            sessionKey = new VaultSessionKey(masterKey);
            masterKey = Array.Empty<byte>();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            if (masterKey.Length > 0)
                CryptographicOperations.ZeroMemory(masterKey);

            CryptographicOperations.ZeroMemory(keyEncryptionKey);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    public VaultHeader RewrapMasterKey(VaultHeader header, string currentPassword, string newPassword)
    {
        ValidatePassword(newPassword);

        if (!TryUnwrapMasterKey(header, currentPassword, out var sessionKey) || sessionKey is null)
            throw new UnauthorizedAccessException("The current vault password is incorrect or the vault header is invalid.");

        using (sessionKey)
        {
            var replacement = new VaultHeader
            {
                FormatVersion = header.FormatVersion,
                VaultIdBase64 = header.VaultIdBase64,
                CipherSuite = header.CipherSuite,
                KdfAlgorithm = header.KdfAlgorithm,
                KdfMemorySizeKiB = header.KdfMemorySizeKiB,
                KdfIterations = header.KdfIterations,
                KdfDegreeOfParallelism = header.KdfDegreeOfParallelism,
                KdfSaltBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize))
            };

            WrapMasterKey(replacement, newPassword, sessionKey.DangerousGetKey());
            return replacement;
        }
    }

    private static void WrapMasterKey(VaultHeader header, string password, byte[] masterKey)
    {
        var salt = Convert.FromBase64String(header.KdfSaltBase64);
        var keyEncryptionKey = DeriveKeyEncryptionKey(password, salt, header);
        var nonce = RandomNumberGenerator.GetBytes(AesGcmNonceSize);
        var ciphertext = new byte[masterKey.Length];
        var tag = new byte[AesGcmTagSize];
        var aad = BuildAssociatedData(header);

        try
        {
            using var aes = new AesGcm(keyEncryptionKey, AesGcmTagSize);
            aes.Encrypt(nonce, masterKey, ciphertext, tag, aad);

            header.WrappedMasterKeyNonceBase64 = Convert.ToBase64String(nonce);
            header.WrappedMasterKeyCiphertextBase64 = Convert.ToBase64String(ciphertext);
            header.WrappedMasterKeyTagBase64 = Convert.ToBase64String(tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(keyEncryptionKey);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static byte[] DeriveKeyEncryptionKey(string password, byte[] salt, VaultHeader header)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = header.KdfMemorySizeKiB,
                Iterations = header.KdfIterations,
                DegreeOfParallelism = header.KdfDegreeOfParallelism
            };

            return argon2.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] BuildAssociatedData(VaultHeader header)
    {
        using var stream = new MemoryStream();

        WriteField(stream, Encoding.ASCII.GetBytes("Rice2kFolderVault"));
        WriteInt32(stream, header.FormatVersion);
        WriteField(stream, Convert.FromBase64String(header.VaultIdBase64));
        WriteField(stream, Encoding.ASCII.GetBytes(header.CipherSuite));
        WriteField(stream, Encoding.ASCII.GetBytes(header.KdfAlgorithm));
        WriteInt32(stream, header.KdfMemorySizeKiB);
        WriteInt32(stream, header.KdfIterations);
        WriteInt32(stream, header.KdfDegreeOfParallelism);
        WriteField(stream, Convert.FromBase64String(header.KdfSaltBase64));

        return stream.ToArray();
    }

    private static void WriteField(Stream stream, byte[] value)
    {
        WriteInt32(stream, value.Length);
        stream.Write(value, 0, value.Length);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void ValidatePassword(string password)
    {
        if (password is null)
            throw new ArgumentNullException(nameof(password));

        if (password.Length < 12)
            throw new ArgumentException("Use at least 12 characters for the vault password.", nameof(password));
    }

    public static void ValidateHeader(VaultHeader header)
    {
        if (header is null)
            throw new ArgumentNullException(nameof(header));

        if (header.FormatVersion != 1)
            throw new InvalidDataException($"Unsupported vault format version: {header.FormatVersion}.");

        if (!string.Equals(header.CipherSuite, "AES-256-GCM", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported vault cipher suite.");

        if (!string.Equals(header.KdfAlgorithm, "argon2id", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported vault KDF.");

        if (header.KdfMemorySizeKiB < 8192 || header.KdfMemorySizeKiB > 1048576)
            throw new InvalidDataException("Vault KDF memory parameter is outside the supported range.");

        if (header.KdfIterations < 1 || header.KdfIterations > 32)
            throw new InvalidDataException("Vault KDF iteration parameter is outside the supported range.");

        if (header.KdfDegreeOfParallelism < 1 || header.KdfDegreeOfParallelism > 32)
            throw new InvalidDataException("Vault KDF parallelism parameter is outside the supported range.");

        if (Convert.FromBase64String(header.VaultIdBase64).Length != VaultIdSize)
            throw new InvalidDataException("Vault identifier is invalid.");

        if (Convert.FromBase64String(header.KdfSaltBase64).Length != SaltSize)
            throw new InvalidDataException("Vault KDF salt is invalid.");

        if (Convert.FromBase64String(header.WrappedMasterKeyNonceBase64).Length != AesGcmNonceSize)
            throw new InvalidDataException("Wrapped master-key nonce is invalid.");

        if (Convert.FromBase64String(header.WrappedMasterKeyCiphertextBase64).Length != MasterKeySize)
            throw new InvalidDataException("Wrapped master-key ciphertext is invalid.");

        if (Convert.FromBase64String(header.WrappedMasterKeyTagBase64).Length != AesGcmTagSize)
            throw new InvalidDataException("Wrapped master-key authentication tag is invalid.");
    }
}
