using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Views;

public partial class CreateVaultWindow : Window
{
    public VaultRegistration? CreatedVault { get; private set; }

    public CreateVaultWindow()
    {
        InitializeComponent();

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        VaultPathInput.Text = Path.Combine(documents, "Personal Vault.rvault");
        Loaded += (_, _) => VaultNameInput.Focus();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Create Rice2k Folder Vault",
            Filter = "Rice2k Folder Vault (*.rvault)|*.rvault",
            DefaultExt = ".rvault",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = Path.GetFileName(VaultPathInput.Text)
        };

        var currentDirectory = Path.GetDirectoryName(VaultPathInput.Text);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            dialog.InitialDirectory = currentDirectory;

        if (dialog.ShowDialog(this) == true)
            VaultPathInput.Text = dialog.FileName;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = VaultNameInput.Text.Trim();
        var path = VaultPathInput.Text.Trim();
        var password = PasswordInput.Password;
        var confirmation = ConfirmPasswordInput.Password;

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWarning("Enter a name for the vault.");
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowWarning("Choose where the .rvault file should be created.");
            return;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ShowWarning("The passwords do not match.");
            return;
        }

        var created = false;

        try
        {
            App.VaultContainers.CreateVault(path, password);
            created = true;

            var header = App.VaultContainers.ReadHeader(path);
            var registration = new VaultRegistration
            {
                DisplayName = name,
                ContainerPath = Path.GetFullPath(path),
                VaultIdBase64 = header.VaultIdBase64
            };

            App.VaultRegistry.Register(registration);
            CreatedVault = registration;

            ClearPasswords();
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            if (created)
            {
                try { File.Delete(path); } catch { }
            }

            ClearPasswords();
            MessageBox.Show(this,
                "The vault could not be created.\n\n" + ex.Message,
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ClearPasswords();
        DialogResult = false;
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(this, message, "Rice2k Folder Vault",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ClearPasswords()
    {
        PasswordInput.Clear();
        ConfirmPasswordInput.Clear();
    }
}
