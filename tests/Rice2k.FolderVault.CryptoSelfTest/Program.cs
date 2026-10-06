using System;
using System.IO;
using System.Security.Cryptography;
using Rice2k.FolderVault.Services;

internal static class Program
{
    private static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "Rice2kFolderVault-SelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Run(root);
            Console.WriteLine("Rice2k Folder Vault crypto self-test: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Rice2k Folder Vault crypto self-test: FAIL");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Test cleanup failure must not hide a crypto-test result.
            }
        }
    }

    private static void Run(string root)
    {
        const string originalPassword = "correct horse battery staple";
        const string changedPassword = "a different secure vault password";

        var crypto = new VaultCryptoService();
        var containers = new VaultContainerService(crypto);
        var path = Path.Combine(root, "test.rvault");

        containers.CreateVault(path, originalPassword);
        Assert(File.Exists(path), "Vault file was not created.");

        var header = containers.ReadHeader(path);
        Assert(header.FormatVersion == 1, "Unexpected vault-format version.");
        Assert(header.CipherSuite == "AES-256-GCM", "Unexpected cipher suite.");
        Assert(header.KdfAlgorithm == "argon2id", "Unexpected KDF.");

        Assert(containers.TryUnlock(path, originalPassword, out var session) && session is not null,
            "Correct password did not unlock the vault.");

        using (session!)
        {
            var metadata = containers.ReadMetadata(path, session);
            Assert(metadata.MetadataVersion == 1, "Unexpected metadata version.");
            Assert(metadata.Entries.Count == 0, "A new vault should have empty metadata.");
        }

        Assert(!containers.TryUnlock(path, "definitely the wrong password", out var wrongSession),
            "Wrong password unexpectedly unlocked the vault.");
        wrongSession?.Dispose();

        var tamperedHeader = containers.ReadHeader(path);
        var tag = Convert.FromBase64String(tamperedHeader.WrappedMasterKeyTagBase64);
        tag[0] ^= 0x01;
        tamperedHeader.WrappedMasterKeyTagBase64 = Convert.ToBase64String(tag);
        Array.Clear(tag, 0, tag.Length);

        Assert(!crypto.TryUnwrapMasterKey(tamperedHeader, originalPassword, out var tamperedHeaderSession),
            "Tampered master-key authentication tag unexpectedly unlocked.");
        tamperedHeaderSession?.Dispose();

        var tamperedPayloadPath = Path.Combine(root, "tampered-payload.rvault");
        File.Copy(path, tamperedPayloadPath);
        using (var tamperedFile = new FileStream(tamperedPayloadPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            tamperedFile.Position = tamperedFile.Length - 1;
            var value = tamperedFile.ReadByte();
            Assert(value >= 0, "Could not read payload byte for tamper test.");
            tamperedFile.Position = tamperedFile.Length - 1;
            tamperedFile.WriteByte((byte)(value ^ 0x01));
            tamperedFile.Flush(true);
        }

        var payloadTamperRejected = false;
        try
        {
            containers.TryUnlock(tamperedPayloadPath, originalPassword, out var tamperedPayloadSession);
            tamperedPayloadSession?.Dispose();
        }
        catch (CryptographicException)
        {
            payloadTamperRejected = true;
        }

        Assert(payloadTamperRejected, "Tampered encrypted metadata was not rejected.");

        containers.ChangePassword(path, originalPassword, changedPassword);

        Assert(!containers.TryUnlock(path, originalPassword, out var oldSession),
            "Old password still unlocked after password change.");
        oldSession?.Dispose();

        Assert(containers.TryUnlock(path, changedPassword, out var changedSession) && changedSession is not null,
            "New password did not unlock after password change.");

        using (changedSession!)
        {
            var metadataAfterRewrap = containers.ReadMetadata(path, changedSession);
            Assert(metadataAfterRewrap.MetadataVersion == 1,
                "Metadata could not be decrypted after master-key rewrap.");
        }

        var duplicateRejected = false;
        try
        {
            containers.CreateVault(path, changedPassword);
        }
        catch (IOException)
        {
            duplicateRejected = true;
        }

        Assert(duplicateRejected, "Existing vault file was overwritten.");

        var corruptPath = Path.Combine(root, "corrupt.rvault");
        File.WriteAllBytes(corruptPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0 });

        var corruptRejected = false;
        try
        {
            containers.ReadHeader(corruptPath);
        }
        catch (InvalidDataException)
        {
            corruptRejected = true;
        }

        Assert(corruptRejected, "Invalid vault signature was accepted.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
