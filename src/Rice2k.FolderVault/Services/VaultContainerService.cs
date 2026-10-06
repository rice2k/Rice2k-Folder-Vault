using System;
using System.Buffers.Binary;
using System.IO;
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

        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(Magic, 0, Magic.Length);
        WriteHeaderLength(stream, headerBytes.Length);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Flush(true);
    }

    public VaultHeader ReadHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadHeader(stream, out _);
    }

    public bool TryUnlock(string path, string password, out VaultSessionKey? sessionKey)
    {
        var header = ReadHeader(path);
        return _crypto.TryUnwrapMasterKey(header, password, out sessionKey);
    }

    public void ChangePassword(string path, string currentPassword, string newPassword)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = fullPath + ".rewrite";

        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var oldHeader = ReadHeader(input, out var payloadOffset);
        var newHeader = _crypto.RewrapMasterKey(oldHeader, currentPassword, newPassword);
        var newHeaderBytes = SerializeHeader(newHeader);

        try
        {
            using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                output.Write(Magic, 0, Magic.Length);
                WriteHeaderLength(output, newHeaderBytes.Length);
                output.Write(newHeaderBytes, 0, newHeaderBytes.Length);

                input.Position = payloadOffset;
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

    private static VaultHeader ReadHeader(Stream stream, out long payloadOffset)
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
