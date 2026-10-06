using System;
using System.Security.Cryptography;

namespace Rice2k.FolderVault.Models;

public sealed class VaultSessionKey : IDisposable
{
    private byte[]? _key;

    internal VaultSessionKey(byte[] key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    public bool IsDisposed => _key is null;

    internal byte[] DangerousGetKey()
    {
        return _key ?? throw new ObjectDisposedException(nameof(VaultSessionKey));
    }

    public void Dispose()
    {
        if (_key is null)
            return;

        CryptographicOperations.ZeroMemory(_key);
        _key = null;
        GC.SuppressFinalize(this);
    }
}
