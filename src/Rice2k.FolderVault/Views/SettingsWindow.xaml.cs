using System.Windows;

namespace Rice2k.FolderVault.Views;

public partial class SettingsWindow : Window
{
    private sealed record TimeoutOption(string Label, int Minutes)
    {
        public override string ToString() => Label;
    }

    public SettingsWindow()
    {
        InitializeComponent();

        AutoLockMinutesComboBox.ItemsSource = new[]
        {
            new TimeoutOption("1 minute", 1),
            new TimeoutOption("5 minutes", 5),
            new TimeoutOption("10 minutes", 10),
            new TimeoutOption("15 minutes", 15),
            new TimeoutOption("30 minutes", 30),
            new TimeoutOption("60 minutes", 60)
        };

        MountPointComboBox.ItemsSource = new[]
        {
            "V:", "W:", "X:", "Y:", "Z:"
        };

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = App.Settings;

        AutoLockEnabledCheckBox.IsChecked = settings.AutoLockEnabled;
        LockWhenWindowsLocksCheckBox.IsChecked = settings.LockWhenWindowsLocks;
        LockWhenSystemSleepsCheckBox.IsChecked = settings.LockWhenSystemSleeps;
        LockWhenUserSignsOutCheckBox.IsChecked = settings.LockWhenUserSignsOut;
        LockWhenScreensaverStartsCheckBox.IsChecked = settings.LockWhenScreensaverStarts;
        LockAtShutdownCheckBox.IsChecked = settings.LockAtShutdownOrRestart;
        OpenExplorerAfterUnlockCheckBox.IsChecked = settings.OpenExplorerAfterUnlock;
        MountPointComboBox.SelectedItem = settings.PreferredMountPoint;
        ShowWarningBeforeLockCheckBox.IsChecked = settings.ShowWarningBeforeLock;

        foreach (var item in AutoLockMinutesComboBox.Items)
        {
            if (item is TimeoutOption option && option.Minutes == settings.AutoLockMinutes)
            {
                AutoLockMinutesComboBox.SelectedItem = option;
                break;
            }
        }

        if (AutoLockMinutesComboBox.SelectedIndex < 0)
        {
            AutoLockMinutesComboBox.SelectedIndex = 2;
        }

        if (MountPointComboBox.SelectedIndex < 0)
            MountPointComboBox.SelectedItem = "V:";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings;

        settings.AutoLockEnabled = AutoLockEnabledCheckBox.IsChecked == true;
        settings.LockWhenWindowsLocks = LockWhenWindowsLocksCheckBox.IsChecked == true;
        settings.LockWhenSystemSleeps = LockWhenSystemSleepsCheckBox.IsChecked == true;
        settings.LockWhenUserSignsOut = LockWhenUserSignsOutCheckBox.IsChecked == true;
        settings.LockWhenScreensaverStarts = LockWhenScreensaverStartsCheckBox.IsChecked == true;
        settings.LockAtShutdownOrRestart = LockAtShutdownCheckBox.IsChecked == true;
        settings.OpenExplorerAfterUnlock = OpenExplorerAfterUnlockCheckBox.IsChecked == true;
        settings.PreferredMountPoint = MountPointComboBox.SelectedItem?.ToString() ?? "V:";
        settings.ShowWarningBeforeLock = ShowWarningBeforeLockCheckBox.IsChecked == true;

        if (AutoLockMinutesComboBox.SelectedItem is TimeoutOption option)
        {
            settings.AutoLockMinutes = option.Minutes;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
