using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultRegistryService
{
    private readonly string _registryPath;
    private readonly List<VaultRegistration> _vaults = new();

    public VaultRegistryService()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rice2k Folder Vault");

        Directory.CreateDirectory(dataDirectory);
        _registryPath = Path.Combine(dataDirectory, "vaults.json");
        Load();
    }

    public IReadOnlyList<VaultRegistration> Vaults => _vaults;

    public VaultRegistration? PrimaryVault => _vaults.FirstOrDefault();

    public void Register(VaultRegistration registration)
    {
        if (registration is null)
            throw new ArgumentNullException(nameof(registration));

        if (string.IsNullOrWhiteSpace(registration.DisplayName))
            throw new ArgumentException("Vault display name is required.", nameof(registration));

        if (string.IsNullOrWhiteSpace(registration.ContainerPath))
            throw new ArgumentException("Vault container path is required.", nameof(registration));

        if (_vaults.Any(v => string.Equals(v.VaultIdBase64, registration.VaultIdBase64, StringComparison.Ordinal)))
            throw new InvalidOperationException("This vault is already registered.");

        _vaults.Add(registration);
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_registryPath))
            return;

        try
        {
            var json = File.ReadAllText(_registryPath, Encoding.UTF8);
            var registrations = JsonSerializer.Deserialize<List<VaultRegistration>>(json);

            if (registrations is not null)
                _vaults.AddRange(registrations.Where(v => !string.IsNullOrWhiteSpace(v.ContainerPath)));
        }
        catch
        {
            // A damaged local registry must not alter or delete any .rvault container.
            // Vault import/recovery UI will be added in a later milestone.
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_vaults, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _registryPath + ".tmp";

        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        File.Move(tempPath, _registryPath, true);
    }
}
