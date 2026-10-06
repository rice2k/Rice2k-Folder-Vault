using System;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultStateService
{
    private VaultSessionKey? _sessionKey;

    public event EventHandler? StateChanged;
    public event EventHandler? Locking;

    public bool IsUnlocked => _sessionKey is not null;
    public DateTimeOffset? UnlockedAt { get; private set; }
    public string LastLockReason { get; private set; } = "Application started";
    public string? ActiveVaultIdBase64 { get; private set; }

    public void Unlock(VaultSessionKey sessionKey, string vaultIdBase64)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        if (string.IsNullOrWhiteSpace(vaultIdBase64))
            throw new ArgumentException("A vault identifier is required.", nameof(vaultIdBase64));

        _sessionKey?.Dispose();
        _sessionKey = sessionKey;
        ActiveVaultIdBase64 = vaultIdBase64;
        UnlockedAt = DateTimeOffset.Now;
        LastLockReason = string.Empty;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Lock(string reason)
    {
        if (_sessionKey is not null)
            Locking?.Invoke(this, EventArgs.Empty);

        _sessionKey?.Dispose();
        _sessionKey = null;
        ActiveVaultIdBase64 = null;
        UnlockedAt = null;
        LastLockReason = reason;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal VaultSessionKey RequireSessionKey()
    {
        return _sessionKey ?? throw new InvalidOperationException("The vault is locked.");
    }

    public TimeSpan? GetRemainingUntilAutoLock(int minutes)
    {
        if (!IsUnlocked || UnlockedAt is null)
            return null;

        var remaining = TimeSpan.FromMinutes(minutes) - (DateTimeOffset.Now - UnlockedAt.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
