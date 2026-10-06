using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultCredentialService
{
    private const int SaltSize = 32;
    private const int VerifierSize = 32;
    private readonly string _credentialPath;

    public VaultCredentialService()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rice2k Folder Vault");

        Directory.CreateDirectory(dataDirectory);
        _credentialPath = Path.Combine(dataDirectory, "credentials.json");
    }

    public bool IsConfigured => File.Exists(_credentialPath);

    public void CreatePassword(string password)
    {
        if (IsConfigured)
            throw new InvalidOperationException("Vault credentials are already configured.");

        ValidateNewPassword(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var record = new VaultCredentialRecord
        {
            SaltBase64 = Convert.ToBase64String(salt)
        };

        var verifier = DeriveVerifier(password, salt, record);
        record.PasswordVerifierBase64 = Convert.ToBase64String(verifier);

        WriteRecord(record);
        CryptographicOperations.ZeroMemory(verifier);
        CryptographicOperations.ZeroMemory(salt);
    }

    public bool VerifyPassword(string password)
    {
        if (!IsConfigured || string.IsNullOrEmpty(password))
            return false;

        var record = ReadRecord();
        var salt = Convert.FromBase64String(record.SaltBase64);
        var expected = Convert.FromBase64String(record.PasswordVerifierBase64);
        var actual = DeriveVerifier(password, salt, record);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    public void ChangePassword(string currentPassword, string newPassword)
    {
        if (!VerifyPassword(currentPassword))
            throw new UnauthorizedAccessException("The current password is incorrect.");

        ValidateNewPassword(newPassword);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var record = new VaultCredentialRecord
        {
            SaltBase64 = Convert.ToBase64String(salt)
        };

        var verifier = DeriveVerifier(newPassword, salt, record);
        record.PasswordVerifierBase64 = Convert.ToBase64String(verifier);

        WriteRecord(record);
        CryptographicOperations.ZeroMemory(verifier);
        CryptographicOperations.ZeroMemory(salt);
    }

    private static byte[] DeriveVerifier(string password, byte[] salt, VaultCredentialRecord record)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = record.DegreeOfParallelism,
            Iterations = record.Iterations,
            MemorySize = record.MemorySizeKiB
        };

        return argon2.GetBytes(VerifierSize);
    }

    private VaultCredentialRecord ReadRecord()
    {
        var json = File.ReadAllText(_credentialPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<VaultCredentialRecord>(json)
            ?? throw new InvalidDataException("Vault credential file is invalid.");
    }

    private void WriteRecord(VaultCredentialRecord record)
    {
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _credentialPath + ".tmp";

        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        File.Move(tempPath, _credentialPath, true);
    }

    private static void ValidateNewPassword(string password)
    {
        if (password.Length < 12)
            throw new ArgumentException("Use at least 12 characters for the vault password.", nameof(password));
    }
}
