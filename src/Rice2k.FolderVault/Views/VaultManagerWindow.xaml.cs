using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Views;

public partial class VaultManagerWindow : Window
{
    private readonly VaultRegistration _vault;
    private string? _currentDirectoryId;

    public VaultManagerWindow(VaultRegistration vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        InitializeComponent();
        VaultTitleText.Text = _vault.DisplayName + " — Contents";
        Loaded += (_, _) => RefreshEntries();
    }

    private void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        try
        {
            var metadata = App.VaultContainers.ReadMetadata(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey());

            EnsureCurrentDirectory(metadata);

            var dialog = new VaultItemNameWindow(
                "New Folder — Rice2k Folder Vault",
                "Create encrypted folder",
                "The folder name and hierarchy will be stored inside encrypted vault metadata.",
                "Create Folder")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            App.VaultContainers.CreateDirectory(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey(),
                _currentDirectoryId!,
                dialog.ItemName);

            RefreshEntries();
        }
        catch (Exception ex)
        {
            ShowOperationError("The folder could not be created.", ex);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        VaultMetadata metadata;
        try
        {
            metadata = App.VaultContainers.ReadMetadata(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey());
            EnsureCurrentDirectory(metadata);
        }
        catch (Exception ex)
        {
            ShowOperationError("The current vault folder could not be opened.", ex);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Add files to " + App.VaultContainers.GetDirectoryPath(metadata, _currentDirectoryId!),
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
                App.VaultContainers.ImportFileToDirectory(
                    _vault.ContainerPath,
                    App.VaultState.RequireSessionKey(),
                    path,
                    _currentDirectoryId!);
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
        if (!EnsureUnlocked() || !TryGetSelected(out var selected))
            return;

        if (selected.IsDirectory)
        {
            MessageBox.Show(
                this,
                "Open the folder and select a file to export. Folder export will be added later.",
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            App.VaultContainers.ExportFileByEntryId(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey(),
                selected.EntryId,
                dialog.FileName,
                overwrite: true);

            MessageBox.Show(this, "The file was exported successfully.", "Rice2k Folder Vault",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowOperationError("The file could not be exported.", ex);
        }
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked() || !TryGetSelected(out var selected))
            return;

        var itemType = selected.IsDirectory ? "folder" : "file";
        var dialog = new VaultItemNameWindow(
            "Rename — Rice2k Folder Vault",
            "Rename protected " + itemType,
            "The new name will be written only to authenticated encrypted metadata.",
            "Rename",
            selected.Name)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            App.VaultContainers.RenameEntry(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey(),
                selected.EntryId,
                dialog.ItemName);

            RefreshEntries();
        }
        catch (Exception ex)
        {
            ShowOperationError("The item could not be renamed.", ex);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked() || !TryGetSelected(out var selected))
            return;

        var message = selected.IsDirectory
            ? "Delete folder '" + selected.Name + "' and everything stored inside it?\n\nAll encrypted file records in that folder tree will be removed from the rewritten container. This cannot be undone without a separate backup."
            : "Delete '" + selected.Name + "' from the vault?\n\nIts encrypted content record will be removed from the rewritten container. This cannot be undone without a separate backup.";

        var result = MessageBox.Show(
            this,
            message,
            selected.IsDirectory ? "Delete Protected Folder" : "Delete Protected File",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            App.VaultContainers.DeleteEntry(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey(),
                selected.EntryId,
                recursive: selected.IsDirectory);

            RefreshEntries();
        }
        catch (Exception ex)
        {
            ShowOperationError("The item could not be deleted.", ex);
        }
    }

    private void CompactButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        try
        {
            var before = new FileInfo(_vault.ContainerPath).Length;

            App.VaultContainers.CompactVault(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey());

            var after = new FileInfo(_vault.ContainerPath).Length;
            var reclaimed = Math.Max(0, before - after);

            MessageBox.Show(
                this,
                "Vault compaction completed.\n\nReclaimed: " + FormatSize(reclaimed),
                "Rice2k Folder Vault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RefreshEntries();
        }
        catch (Exception ex)
        {
            ShowOperationError("The vault could not be compacted.", ex);
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureUnlocked())
            return;

        try
        {
            var metadata = App.VaultContainers.ReadMetadata(
                _vault.ContainerPath,
                App.VaultState.RequireSessionKey());

            EnsureCurrentDirectory(metadata);

            if (string.Equals(_currentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal))
                return;

            var currentDirectory = metadata.Entries.FirstOrDefault(e =>
                string.Equals(e.EntryId, _currentDirectoryId, StringComparison.Ordinal) &&
                string.Equals(e.EntryType, "directory", StringComparison.Ordinal));

            _currentDirectoryId = currentDirectory?.ParentDirectoryId ?? metadata.RootDirectoryId;
            RefreshEntries();
        }
        catch (Exception ex)
        {
            ShowOperationError("The parent folder could not be opened.", ex);
        }
    }

    private void EntryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntryList.SelectedItem is not VaultEntryRow selected || !selected.IsDirectory)
            return;

        _currentDirectoryId = selected.EntryId;
        RefreshEntries();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshEntries()
    {
        if (!EnsureUnlocked())
            return;

        try
        {
            var sessionKey = App.VaultState.RequireSessionKey();
            var metadata = App.VaultContainers.ReadMetadata(_vault.ContainerPath, sessionKey);
            EnsureCurrentDirectory(metadata);

            CurrentPathText.Text = App.VaultContainers.GetDirectoryPath(metadata, _currentDirectoryId!);

            EntryList.ItemsSource = App.VaultContainers.ListDirectory(
                    _vault.ContainerPath,
                    sessionKey,
                    _currentDirectoryId)
                .Select(e => new VaultEntryRow
                {
                    EntryId = e.EntryId,
                    Name = e.Name,
                    EntryType = e.EntryType,
                    Type = string.Equals(e.EntryType, "directory", StringComparison.Ordinal)
                        ? "Folder"
                        : "File",
                    Size = string.Equals(e.EntryType, "directory", StringComparison.Ordinal)
                        ? string.Empty
                        : FormatSize(e.PlaintextLength),
                    Modified = FormatModified(e.LastWriteTimeUtcTicks)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            ShowOperationError("Vault contents could not be read.", ex);
        }
    }

    private void EnsureCurrentDirectory(VaultMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(_currentDirectoryId))
        {
            _currentDirectoryId = metadata.RootDirectoryId;
            return;
        }

        if (string.Equals(_currentDirectoryId, metadata.RootDirectoryId, StringComparison.Ordinal))
            return;

        var stillExists = metadata.Entries.Any(e =>
            string.Equals(e.EntryId, _currentDirectoryId, StringComparison.Ordinal) &&
            string.Equals(e.EntryType, "directory", StringComparison.Ordinal));

        if (!stillExists)
            _currentDirectoryId = metadata.RootDirectoryId;
    }

    private bool TryGetSelected(out VaultEntryRow selected)
    {
        if (EntryList.SelectedItem is VaultEntryRow row)
        {
            selected = row;
            return true;
        }

        selected = null!;
        MessageBox.Show(this, "Select a file or folder first.", "Rice2k Folder Vault",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
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

    private void ShowOperationError(string message, Exception ex)
    {
        MessageBox.Show(this,
            message + "\n\n" + ex.Message,
            "Rice2k Folder Vault",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
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
        public string EntryId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string EntryType { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public string Modified { get; init; } = string.Empty;

        public bool IsDirectory =>
            string.Equals(EntryType, "directory", StringComparison.Ordinal);
    }
}
