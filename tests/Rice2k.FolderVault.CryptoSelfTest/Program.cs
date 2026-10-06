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
                // Cleanup failure must not hide the crypto-test result.
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

        Assert(!containers.TryUnlock(path, "definitely the wrong password", out var wrongSession),
            "Wrong password unexpectedly unlocked the vault.");
        wrongSession?.Dispose();

        TestWrappedMasterKeyTamper(crypto, containers, path, originalPassword);
        TestMetadataTamper(containers, path, originalPassword, root);

        var sourcePath = Path.Combine(root, "import-source.bin");
        var firstExportPath = Path.Combine(root, "first-export.bin");
        var rewrapExportPath = Path.Combine(root, "rewrap-export.bin");

        Assert(containers.TryUnlock(path, originalPassword, out var session) && session is not null,
            "Correct password did not unlock the vault.");

        using (session!)
        {
            var metadata = containers.ReadMetadata(path, session);
            Assert(metadata.MetadataVersion == 1, "Unexpected metadata version.");
            Assert(metadata.Entries.Count == 0, "A new vault should have empty metadata.");

            TestStandaloneContentService(header, session);

            var importBytes = RandomNumberGenerator.GetBytes((2 * 1024 * 1024) + 177_777);
            try
            {
                File.WriteAllBytes(sourcePath, importBytes);

                var imported = containers.ImportFile(path, session, sourcePath, "inside.bin");
                Assert(imported.Name == "inside.bin", "Imported metadata name changed.");
                Assert(imported.PlaintextLength == importBytes.LongLength, "Imported length changed.");

                var metadataAfterImport = containers.ReadMetadata(path, session);
                Assert(metadataAfterImport.Entries.Count == 1, "Imported file was not added to metadata.");
                Assert(metadataAfterImport.Revision == 1, "Metadata revision did not advance.");

                containers.ExportFile(path, session, "inside.bin", firstExportPath);
                var exportedBytes = File.ReadAllBytes(firstExportPath);
                try
                {
                    Assert(importBytes.AsSpan().SequenceEqual(exportedBytes),
                        "Persistent encrypted import/export did not round-trip exactly.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(exportedBytes);
                }

                var duplicateNameRejected = false;
                try
                {
                    containers.ImportFile(path, session, sourcePath, "inside.bin");
                }
                catch (IOException)
                {
                    duplicateNameRejected = true;
                }

                Assert(duplicateNameRejected, "Duplicate root filename was accepted.");

                TestStoredFileTamper(containers, path, originalPassword, root);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(importBytes);
            }
        }

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
            Assert(metadataAfterRewrap.Entries.Count == 1,
                "File metadata was lost during password rewrap.");

            containers.ExportFile(path, changedSession, "inside.bin", rewrapExportPath);

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var rewrappedExportBytes = File.ReadAllBytes(rewrapExportPath);
            try
            {
                Assert(sourceBytes.AsSpan().SequenceEqual(rewrappedExportBytes),
                    "Encrypted file payload changed or became unreadable after password rewrap.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sourceBytes);
                CryptographicOperations.ZeroMemory(rewrappedExportBytes);
            }
        }

        var duplicateVaultRejected = false;
        try
        {
            containers.CreateVault(path, changedPassword);
        }
        catch (IOException)
        {
            duplicateVaultRejected = true;
        }

        Assert(duplicateVaultRejected, "Existing vault file was overwritten.");

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

    private static void TestWrappedMasterKeyTamper(
        VaultCryptoService crypto,
        VaultContainerService containers,
        string path,
        string password)
    {
        var tamperedHeader = containers.ReadHeader(path);
        var tag = Convert.FromBase64String(tamperedHeader.WrappedMasterKeyTagBase64);
        tag[0] ^= 0x01;
        tamperedHeader.WrappedMasterKeyTagBase64 = Convert.ToBase64String(tag);
        CryptographicOperations.ZeroMemory(tag);

        Assert(!crypto.TryUnwrapMasterKey(tamperedHeader, password, out var session),
            "Tampered master-key authentication tag unexpectedly unlocked.");
        session?.Dispose();
    }

    private static void TestMetadataTamper(
        VaultContainerService containers,
        string path,
        string password,
        string root)
    {
        var tamperedMetadataPath = Path.Combine(root, "tampered-metadata.rvault");
        File.Copy(path, tamperedMetadataPath);

        using (var file = new FileStream(tamperedMetadataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            file.Position = file.Length - 1;
            var value = file.ReadByte();
            Assert(value >= 0, "Could not read metadata byte for tamper test.");
            file.Position = file.Length - 1;
            file.WriteByte((byte)(value ^ 0x01));
            file.Flush(true);
        }

        var rejected = false;
        try
        {
            containers.TryUnlock(tamperedMetadataPath, password, out var session);
            session?.Dispose();
        }
        catch (CryptographicException)
        {
            rejected = true;
        }

        Assert(rejected, "Tampered encrypted metadata was not rejected.");
    }

    private static void TestStandaloneContentService(
        Rice2k.FolderVault.Models.VaultHeader header,
        Rice2k.FolderVault.Models.VaultSessionKey session)
    {
        var contentService = new VaultContentService();
        var originalBytes = RandomNumberGenerator.GetBytes((2 * 1024 * 1024) + 333_333);

        try
        {
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
            try
            {
                Assert(originalBytes.AsSpan().SequenceEqual(roundTripBytes),
                    "Chunked encrypted content did not round-trip exactly.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(roundTripBytes);
            }

            var tamperedRecordBytes = encryptedRecord.ToArray();
            try
            {
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
            }
            finally
            {
                CryptographicOperations.ZeroMemory(tamperedRecordBytes);
            }

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
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalBytes);
        }
    }

    private static void TestStoredFileTamper(
        VaultContainerService containers,
        string path,
        string password,
        string root)
    {
        var tamperedPath = Path.Combine(root, "tampered-file-record.rvault");
        File.Copy(path, tamperedPath);

        using (var file = new FileStream(tamperedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            file.Position = file.Length - 1;
            var value = file.ReadByte();
            Assert(value >= 0, "Could not read content byte for tamper test.");
            file.Position = file.Length - 1;
            file.WriteByte((byte)(value ^ 0x01));
            file.Flush(true);
        }

        Assert(containers.TryUnlock(tamperedPath, password, out var session) && session is not null,
            "Content-record tampering should not corrupt the separately authenticated metadata.");

        using (session!)
        {
            var rejected = false;
            var outputPath = Path.Combine(root, "tampered-export.bin");

            try
            {
                containers.ExportFile(tamperedPath, session, "inside.bin", outputPath);
            }
            catch (CryptographicException)
            {
                rejected = true;
            }

            Assert(rejected, "Tampered stored file record was not rejected during export.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
