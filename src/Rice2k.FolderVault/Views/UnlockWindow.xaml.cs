using System;
using System.IO;
using System.Windows;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Views;

public partial class UnlockWindow : Window
{
    private readonly VaultRegistration _vault;
    private VaultSessionKey? _sessionKey;

    public UnlockWindow(VaultRegistration vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));

        InitializeComponent();
        VaultNameText.Text = $"{_vault.DisplayName} is locked";
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public VaultSessionKey? TakeSessionKey()
    {
        var key = _sessionKey;
        _sessionKey = null;
        return key;
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        var password = ShowPasswordCheckBox.IsChecked == true
            ? VisiblePasswordInput.Text
            : PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(this, "Enter your vault password.", "Password Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (!App.VaultContainers.TryUnlock(_vault.ContainerPath, password, out var sessionKey) || sessionKey is null)
            {
                ClearPasswordFields();
                MessageBox.Show(this,
                    "The password is incorrect, or the vault header failed authentication.",
                    "Unlock Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                PasswordInput.Focus();
                return;
            }

            _sessionKey = sessionKey;
            ClearPasswordFields();
            DialogResult = true;
        }
        catch (FileNotFoundException)
        {
            ClearPasswordFields();
            MessageBox.Show(this,
                "The registered .rvault file could not be found. The vault was not unlocked.",
                "Vault File Missing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception)
        {
            ClearPasswordFields();
            MessageBox.Show(this,
                "The vault could not be opened or its header is invalid. The vault was not unlocked.",
                "Vault Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _sessionKey?.Dispose();
        _sessionKey = null;
        ClearPasswordFields();
        DialogResult = false;
    }

    private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowPasswordCheckBox.IsChecked == true)
        {
            VisiblePasswordInput.Text = PasswordInput.Password;
            PasswordInput.Clear();
            PasswordInput.Visibility = Visibility.Collapsed;
            VisiblePasswordInput.Visibility = Visibility.Visible;
            VisiblePasswordInput.Focus();
            VisiblePasswordInput.CaretIndex = VisiblePasswordInput.Text.Length;
        }
        else
        {
            PasswordInput.Password = VisiblePasswordInput.Text;
            VisiblePasswordInput.Clear();
            VisiblePasswordInput.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordInput.Focus();
        }
    }

    private void ClearPasswordFields()
    {
        PasswordInput.Clear();
        VisiblePasswordInput.Clear();
    }
}
