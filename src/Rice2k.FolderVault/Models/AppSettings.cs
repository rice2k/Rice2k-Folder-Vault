namespace Rice2k.FolderVault.Models;

public sealed class AppSettings
{
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 10;
    public bool LockWhenWindowsLocks { get; set; } = true;
    public bool LockWhenSystemSleeps { get; set; } = true;
    public bool LockWhenUserSignsOut { get; set; } = true;
    public bool LockWhenScreensaverStarts { get; set; }
    public bool LockAtShutdownOrRestart { get; set; } = true;
    public bool ShowWarningBeforeLock { get; set; } = true;
    public int WarningSeconds { get; set; } = 30;
    public bool OpenExplorerAfterUnlock { get; set; } = true;
    public string PreferredMountPoint { get; set; } = "V:";
}
