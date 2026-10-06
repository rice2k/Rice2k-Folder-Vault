using System.Windows;

namespace Rice2k.FolderVault.Views;

public partial class UnlockWindow : Window
{
    public UnlockWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        var password = ShowPasswordCheckBox.IsChecked == true
            ? VisiblePasswordInput.Text
            : PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                this,
                "Enter a password to continue with the interface preview.",
                "Password Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ClearPasswordFields();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
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
