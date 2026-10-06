namespace Rice2k.FolderVault.Models;

public sealed class VaultRegistration
{
    public string DisplayName { get; set; } = "Personal Vault";
    public string ContainerPath { get; set; } = string.Empty;
    public string VaultIdBase64 { get; set; } = string.Empty;
}
