namespace Rice2k.FolderVault.Services;

public sealed class VaultStateService
{
    public event EventHandler? StateChanged;

    public bool IsUnlocked { get; private set; }
    public DateTimeOffset? UnlockedAt { get; private set; }
    public string LastLockReason { get; private set; } = "Application started";

    public void UnlockPreview()
    {
        IsUnlocked = true;
        UnlockedAt = DateTimeOffset.Now;
        LastLockReason = string.Empty;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Lock(string reason)
    {
        IsUnlocked = false;
        UnlockedAt = null;
        LastLockReason = reason;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public TimeSpan? GetRemainingUntilAutoLock(int minutes)
    {
        if (!IsUnlocked || UnlockedAt is null)
        {
            return null;
        }

        var remaining = TimeSpan.FromMinutes(minutes) - (DateTimeOffset.Now - UnlockedAt.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
