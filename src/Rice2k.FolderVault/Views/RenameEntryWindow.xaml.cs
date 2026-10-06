using System.Windows;

namespace Rice2k.FolderVault.Views;

public partial class RenameEntryWindow : Window
{
    public string NewName => NameInput.Text.Trim();

    public RenameEntryWindow(string currentName)
    {
        InitializeComponent();
        NameInput.Text = currentName;
        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            MessageBox.Show(this, "Enter a file name.", "Rice2k Folder Vault",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
