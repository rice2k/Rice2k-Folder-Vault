using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
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

        var dialog = new UnlockWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            App.VaultState.UnlockPreview();
        }
    }

    public void OpenSettings()
    {
        var dialog = new SettingsWindow
        {
            Owner = this
        };

        dialog.ShowDialog();
        RefreshState();
    }

    public void AllowApplicationExit()
    {
        _allowApplicationExit = true;
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => BeginUnlock();

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        App.VaultState.Lock("Manual lock");
    }

    private void OpenVaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!App.VaultState.IsUnlocked)
        {
            BeginUnlock();
            return;
        }

        MessageBox.Show(
            this,
            "The encrypted virtual-drive mount will be connected in the storage/filesystem milestone. This alpha currently demonstrates the control workflow only.",
            "Rice2k Folder Vault",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void RefreshState()
    {
        var unlocked = App.VaultState.IsUnlocked;

        StatusText.Text = unlocked ? "Unlocked" : "Locked";
        StatusBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(unlocked ? "#1E6B3A" : "#5A2530"));

        StatusDetailText.Text = unlocked
            ? "Vault control session is open. Encryption/mount engine is not connected in this alpha."
            : "Vault contents are not mounted.";

        UnlockButton.IsEnabled = !unlocked;
        OpenVaultButton.IsEnabled = unlocked;
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

        AutoLockText.Text = $"Preview auto-lock in {remaining.Value:mm\\:ss}";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowApplicationExit)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
