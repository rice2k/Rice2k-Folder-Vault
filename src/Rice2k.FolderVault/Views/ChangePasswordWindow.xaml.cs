using System;
using System.Windows;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly VaultRegistration _vault;

    public ChangePasswordWindow(VaultRegistration vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));

        InitializeComponent();
        VaultNameText.Text = _vault.DisplayName + " — Change Password";
        Loaded += (_, _) => CurrentPasswordInput.Focus();
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPassword = CurrentPasswordInput.Password;
        var newPassword = NewPasswordInput.Password;
        var confirmation = ConfirmPasswordInput.Password;

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            ShowWarning("Enter the current vault password.");
            return;
        }

        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            ShowWarning("The new passwords do not match.");
            ClearNewPasswordFields();
            return;
        }

        try
        {
            App.VaultContainers.ChangePassword(
                _vault.ContainerPath,
                currentPassword,
                newPassword);

            ClearAllPasswordFields();

            MessageBox.Show(
                this,
                "The vault password was changed successfully. The encrypted file payload did not need to be re-encrypted.",
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (UnauthorizedAccessException)
        {
            ClearAllPasswordFields();
            ShowWarning("The current password is incorrect.");
            CurrentPasswordInput.Focus();
        }
        catch (ArgumentException ex)
        {
            ClearNewPasswordFields();
            ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            ClearAllPasswordFields();

            MessageBox.Show(
                this,
                "The vault password could not be changed. The existing vault has been left in place.\n\n" + ex.Message,
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ClearAllPasswordFields();
        DialogResult = false;
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Rice2k Folder Vault",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ClearNewPasswordFields()
    {
        NewPasswordInput.Clear();
        ConfirmPasswordInput.Clear();
    }

    private void ClearAllPasswordFields()
    {
        CurrentPasswordInput.Clear();
        ClearNewPasswordFields();
    }
}
