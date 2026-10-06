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

            var contentService = new VaultContentService();
            var originalBytes = RandomNumberGenerator.GetBytes((2 * 1024 * 1024) + 333_333);
            using var plaintextSource = new MemoryStream(originalBytes, writable: false);
            using var encryptedRecord = new MemoryStream();

            var contentInfo = contentService.WriteEncryptedContent(
                encryptedRecord,
                header,
                session,
                plaintextSource,
                originalBytes.LongLength);

            Assert(contentInfo.ChunkCount == 3, "Unexpected content chunk count.");

            encryptedRecord.Position = 0;
            using var decryptedRecord = new MemoryStream();
            var readInfo = contentService.ReadEncryptedContent(
                encryptedRecord,
                header,
                session,
                decryptedRecord,
                contentInfo.RecordIdBase64);

            Assert(readInfo.PlaintextLength == originalBytes.LongLength,
                "Round-trip content length changed.");

            var roundTripBytes = decryptedRecord.ToArray();
            Assert(originalBytes.AsSpan().SequenceEqual(roundTripBytes),
                "Chunked encrypted content did not round-trip exactly.");

            var tamperedRecordBytes = encryptedRecord.ToArray();
            tamperedRecordBytes[^1] ^= 0x01;
            using var tamperedRecord = new MemoryStream(tamperedRecordBytes, writable: false);
            using var tamperedOutput = new MemoryStream();

            var contentTamperRejected = false;
            try
            {
                contentService.ReadEncryptedContent(
                    tamperedRecord,
                    header,
                    session,
                    tamperedOutput,
                    contentInfo.RecordIdBase64);
            }
            catch (CryptographicException)
            {
                contentTamperRejected = true;
            }

            Assert(contentTamperRejected, "Tampered encrypted file content was not rejected.");

            using var emptySource = new MemoryStream(Array.Empty<byte>(), writable: false);
            using var emptyEncrypted = new MemoryStream();
            var emptyInfo = contentService.WriteEncryptedContent(
                emptyEncrypted,
                header,
                session,
                emptySource,
                0);

            emptyEncrypted.Position = 0;
            using var emptyOutput = new MemoryStream();
            contentService.ReadEncryptedContent(
                emptyEncrypted,
                header,
                session,
                emptyOutput,
                emptyInfo.RecordIdBase64);

            Assert(emptyOutput.Length == 0, "Empty encrypted file did not round-trip as empty.");

            CryptographicOperations.ZeroMemory(originalBytes);
            CryptographicOperations.ZeroMemory(roundTripBytes);
            CryptographicOperations.ZeroMemory(tamperedRecordBytes);
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
