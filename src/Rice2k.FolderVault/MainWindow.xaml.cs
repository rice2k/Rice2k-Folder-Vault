using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Rice2k.FolderVault.Models;
using Rice2k.FolderVault.Views;

namespace Rice2k.FolderVault;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _statusTimer;
    private bool _allowApplicationExit;

    public MainWindow()
    {
        InitializeComponent();

        App.VaultState.StateChanged += (_, _) => Dispatcher.Invoke(RefreshState);
        App.VaultMounts.StateChanged += (_, _) => Dispatcher.Invoke(RefreshState);

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateAutoLockCountdown();
        _statusTimer.Start();

        RefreshState();
    }

    public void BeginUnlock()
    {
        if (App.VaultState.IsUnlocked)
        {
            Activate();
            return;
        }

        var vault = App.VaultRegistry.PrimaryVault;
        if (vault is null)
        {
            MessageBox.Show(this, "No vault is registered.", "Rice2k Folder Vault",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new UnlockWindow(vault) { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            var sessionKey = dialog.TakeSessionKey();
            if (sessionKey is not null)
                App.VaultState.Unlock(sessionKey, vault.VaultIdBase64);
        }
    }

    public void OpenSettings()
    {
        var dialog = new SettingsWindow { Owner = this };
        dialog.ShowDialog();
        RefreshState();
    }

    public void OpenVaultManager()
    {
        if (!App.VaultState.IsUnlocked)
        {
            BeginUnlock();
            return;
        }

        var vault = App.VaultRegistry.PrimaryVault;
        if (vault is null)
            return;

        var dialog = new VaultManagerWindow(vault) { Owner = this };
        dialog.ShowDialog();
        RefreshState();
    }

    public void OpenChangePassword()
    {
        if (!App.VaultState.IsUnlocked)
        {
            BeginUnlock();
            return;
        }

        var vault = App.VaultRegistry.PrimaryVault;
        if (vault is null)
            return;

        var dialog = new ChangePasswordWindow(vault) { Owner = this };
        dialog.ShowDialog();
        RefreshState();
    }

    public void MountExplorerDrive()
    {
        if (!App.VaultState.IsUnlocked)
        {
            BeginUnlock();
            if (!App.VaultState.IsUnlocked)
                return;
        }

        var vault = App.VaultRegistry.PrimaryVault;
        if (vault is null)
            return;

        try
        {
            App.VaultMounts.MountReadOnly(
                vault,
                App.VaultState.RequireSessionKey(),
                App.Settings.PreferredMountPoint);

            RefreshState();

            var mountPoint = App.VaultMounts.MountPoint;
            if (!string.IsNullOrWhiteSpace(mountPoint))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = """ + mountPoint + """,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "The encrypted drive mounted successfully, but File Explorer could not be opened.\n\n" + ex.Message,
                        "Rice2k Folder Vault",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "The read-only Explorer drive could not be mounted.\n\n" + ex.Message,
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void AllowApplicationExit() => _allowApplicationExit = true;

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => BeginUnlock();

    private void LockButton_Click(object sender, RoutedEventArgs e) =>
        App.VaultState.Lock("Manual lock");

    private void OpenVaultButton_Click(object sender, RoutedEventArgs e) => OpenVaultManager();

    private void MountDriveButton_Click(object sender, RoutedEventArgs e) => MountExplorerDrive();

    private void UnmountDriveButton_Click(object sender, RoutedEventArgs e)
    {
        App.VaultMounts.Unmount();
        RefreshState();
    }

    private void VaultManagerButton_Click(object sender, RoutedEventArgs e) => OpenVaultManager();

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e) => OpenChangePassword();

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void RefreshState()
    {
        var vault = App.VaultRegistry.PrimaryVault;
        var unlocked = App.VaultState.IsUnlocked;

        VaultNameText.Text = vault?.DisplayName ?? "No Vault";
        StatusText.Text = unlocked ? "Unlocked" : "Locked";
        StatusBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(unlocked ? "#1E6B3A" : "#5A2530"));

        var mounted = App.VaultMounts.IsMounted;
        var mountPoint = App.VaultMounts.MountPoint;

        StatusDetailText.Text = unlocked
            ? mounted
                ? "Vault is unlocked and mounted read-only in File Explorer."
                : "Vault is unlocked. Mount the read-only Explorer drive when you want normal folder browsing."
            : "Vault master key is not available in memory.";

        MountPointText.Text = mountPoint ?? App.Settings.PreferredMountPoint;
        MountDetailText.Text = mounted
            ? "Mounted read-only"
            : "Not mounted";

        UnlockButton.IsEnabled = !unlocked && vault is not null;
        OpenVaultButton.IsEnabled = unlocked;
        MountDriveButton.IsEnabled = unlocked && !mounted;
        UnmountDriveButton.IsEnabled = mounted;
        VaultManagerButton.IsEnabled = unlocked;
        ChangePasswordButton.IsEnabled = unlocked;
        LockButton.IsEnabled = unlocked;

        UpdateAutoLockCountdown();
    }

    private void UpdateAutoLockCountdown()
    {
        if (!App.VaultState.IsUnlocked || !App.Settings.AutoLockEnabled)
        {
            AutoLockText.Text = App.VaultState.IsUnlocked ? "Auto-lock disabled" : string.Empty;
            return;
        }

        var remaining = App.VaultState.GetRemainingUntilAutoLock(App.Settings.AutoLockMinutes);

        if (remaining is null)
        {
            AutoLockText.Text = string.Empty;
            return;
        }

        if (remaining.Value <= TimeSpan.Zero)
        {
            App.VaultState.Lock("Auto-lock timer");
            return;
        }

        AutoLockText.Text = "Auto-lock in " + remaining.Value.ToString(@"mm\:ss");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowApplicationExit)
            return;

        e.Cancel = true;
        Hide();
    }
}
