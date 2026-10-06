using System;
using System.Windows;

namespace Rice2k.FolderVault.Views;

public partial class FirstRunSetupWindow : Window
{
    public FirstRunSetupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        var confirmation = ConfirmPasswordInput.Password;

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "The passwords do not match.", "Password Mismatch",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            App.Credentials.CreatePassword(password);
            PasswordInput.Clear();
            ConfirmPasswordInput.Clear();
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Password Requirements",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The vault password could not be created.\n\n" + ex.Message,
                "Rice2k Folder Vault", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        PasswordInput.Clear();
        ConfirmPasswordInput.Clear();
        DialogResult = false;
    }
}
