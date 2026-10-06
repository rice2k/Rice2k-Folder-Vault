using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Views;

public partial class VaultManagerWindow : Window
{
    private readonly VaultRegistration _vault;

    public VaultManagerWindow(VaultRegistration vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        InitializeComponent();
        VaultTitleText.Text = _vault.DisplayName + " — Contents";
        Loaded += (_, _) => RefreshEntries();
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Add files to Rice2k Folder Vault",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var failures = new List<string>();

        foreach (var path in dialog.FileNames)
        {
            try
            {
                App.VaultContainers.ImportFile(
                    _vault.ContainerPath,
                    App.VaultState.RequireSessionKey(),
                    path);
            }
            catch (Exception ex)
            {
                failures.Add(Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        RefreshEntries();

        if (failures.Count > 0)
        {
            MessageBox.Show(
                this,
                "Some files could not be added:\n\n" + string.Join("\n", failures),
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        if (EntryList.SelectedItem is not VaultEntryRow selected)
        {
            MessageBox.Show(this, "Select a file to export.", "Rice2k Folder Vault",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export file from Rice2k Folder Vault",
            FileName = selected.Name,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            App.VaultContainers.ExportFile(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey(),
                selected.Name,
                dialog.FileName,
                overwrite: true);

            MessageBox.Show(this, "The file was exported successfully.", "Rice2k Folder Vault",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "The file could not be exported.\n\n" + ex.Message,
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshEntries()
    {
        if (!EnsureUnlocked())
            return;

        try
        {
            var metadata = App.VaultContainers.ReadMetadata(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey());

            EntryList.ItemsSource = metadata.Entries
                .Where(e => string.Equals(e.EntryType, "file", StringComparison.Ordinal))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => new VaultEntryRow
                {
                    Name = e.Name,
                    Type = "File",
                    Size = FormatSize(e.PlaintextLength),
                    Modified = FormatModified(e.LastWriteTimeUtcTicks)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Vault contents could not be read.\n\n" + ex.Message,
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool EnsureUnlocked()
    {
        if (App.VaultState.IsUnlocked)
            return true;

        MessageBox.Show(this,
            "The vault is locked. Close this window and unlock it before managing files.",
            "Rice2k Folder Vault",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";

        var kib = bytes / 1024d;
        if (kib < 1024)
            return kib.ToString("0.0") + " KB";

        var mib = kib / 1024d;
        if (mib < 1024)
            return mib.ToString("0.0") + " MB";

        return (mib / 1024d).ToString("0.00") + " GB";
    }

    private static string FormatModified(long ticks)
    {
        if (ticks <= 0)
            return string.Empty;

        try
        {
            return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("g");
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class VaultEntryRow
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public string Modified { get; init; } = string.Empty;
    }
}
