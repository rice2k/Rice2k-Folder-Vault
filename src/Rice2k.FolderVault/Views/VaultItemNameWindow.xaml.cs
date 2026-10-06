using System.Windows;

namespace Rice2k.FolderVault.Views;

public partial class VaultItemNameWindow : Window
{
    public string ItemName => NameInput.Text.Trim();

    public VaultItemNameWindow(
        string title,
        string heading,
        string description,
        string actionLabel,
        string initialName = "")
    {
        InitializeComponent();

        Title = title;
        HeadingText.Text = heading;
        DescriptionText.Text = description;
        ConfirmButton.Content = actionLabel;
        NameInput.Text = initialName;

        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            MessageBox.Show(
                this,
                "Enter a name.",
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
